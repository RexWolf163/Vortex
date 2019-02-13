<?php
namespace app\Http\Controllers\Vortex;


use App\Http\Controllers\Controller;
use App\Vortex\Vortex;
use Carbon\Carbon;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Input;
use Illuminate\Support\Facades\Response;

class VortexController extends Controller
{
    public function ping()
    {

        $vortexHashBuster = env('VORTEX_HASH_BUST', false);

        $responce = '';
        try {
            $data = json_decode(Input::get('data'), true);
            $sessionID = Input::get('_session');
            $vframes = (isset($data['frames'])) ? $data['frames'] : [];
            $vlists = (isset($data['lists'])) ? $data['lists'] : [];

            /*
             * Фреймы заносятся в память сессии.
             * После этого производится перерасчет всех фреймов ИЗ СЕССИИ
             * Клиенту возвращаются те данные, которые изменились
             */
            $sessionsList = session('sessions', []);

            /*
             * Очистка устаревших данных.
             * Условие очистки - от lastPing до Carbon::now более 30 минут
             */
            foreach ($sessionsList as $tempSessionID => $sessionData) {
                /** @var Carbon $lastPing */
                if (isset($lastPing) && $lastPing->diffInMinutes(Carbon::now()) > 30) unset($sessionsList[$tempSessionID]);
            }

            $sessionData = (isset($sessionsList[$sessionID])) ? $sessionsList[$sessionID] : ['vlists' => [], 'vframes' => [], '_vortexBustList' => []];
            if ($vortexHashBuster) $bustList = $sessionData['_vortexBustList'];
            $lastPing = (isset($sessionData['lastPing'])) ? $sessionData['lastPing'] : Carbon::now();
            $sessionLists = $sessionData['vlists'];
            if (count($vlists) > 0) {
                foreach ($vlists as $key => $vlist) {
                    $check = (isset($sessionLists[$key])) ? $sessionLists[$key]['checkfilter'] : "{}";
                    $sessionLists[$key] = $vlist;
                    $sessionLists[$key]['checkfilter'] = $check;
                }
            }

            $sessionFrames = $sessionData['vframes'];
            if (count($vframes) > 0) {
                foreach ($vframes as $key => $vframe) {
                    $sessionFrames[$key] = $vframe;
                }
            }

            $responceLists = [];
            $responceFrames = [];
            $newBustList = [];
            foreach ($sessionLists as $key => $vlist) {
                try {
                    $arr = explode('.', $vlist['vid']);
                    if (count($arr) < 2) {
                        unset($sessionLists[$key]);
                        continue;
                    }
                    /** @var Vortex $class */
                    $class = str_replace('_', '\\', $arr[0]);
                    $vid = $arr[1];
                    $hash = (isset($vlist['hash'])) ? $vlist['hash'] : "";

                    /* Если фильтр не изменялся, то он берется такой же как и старый */
                    $newFilter = (isset($vlist['filter'])) ? $vlist['filter'] : $vlist['checkfilter'];
                    unset($sessionLists[$key]['filter']);
                    $filter = (isset($sessionLists[$key]['checkfilter'])) ? $sessionLists[$key]['checkfilter'] : "{}";
                    $sessionLists[$key]['checkfilter'] = $newFilter;

                    $filterTemp = json_decode($newFilter, true);

                    /**
                     * Если активирован ускоритель (VORTEX_HASH_BUST в .env файле выставлен в true),
                     * то производится сличение значения последнего изменения таблицы
                     * (хранимого в таблице vortex_change_store) с тем что хранится в сессии.
                     * Если они совпадают, то значит можно пропустить фазу формирования списков для данной таблицы,
                     * так как хэш все равно не поменялся.
                     * Это позволит ускорить обсчет списков, если в таблицу не вносятся изменения (холостой ход)
                     */
                    $arIntersect = null;
                    if ($vortexHashBuster) {
                        if ($temp = DB::table('vortex_change_store')->where('table', $class)->first()) {
                            if (isset($bustList[$class])) {
                                if ($bustList[$class] == $temp->updated_at && $filter == $newFilter && $hash != '') continue;
                            }
                            /* Запоминаем то значение, которым нужно будет обновить данные в сессии */
                            $newBustList[$class] = $temp->updated_at;
                            /*
                             * Если фильтр менялся, то ускорение по частичному изменению списка будет некорректно,
                             * поэтому, в этом случае мы идем стандартным путем, без ускорения
                             */
                            if (isset($bustList[$class]) && isset($sessionLists[$key]) && $filter == $newFilter) {
                                /* Составляется список объектов, попавших под изменения */
                                $updated = $class::where('updated_at', '>', $bustList[$class])->lists('id')->toArray();
                                /*
                                 * Берем сохраненный список объектов и смотрим совпадения, создавая новый массив
                                 * Далее проведем формирование нового списка с добавлением фильтрации по дате изменения
                                 * более $bustList[$class] или whereIn('id',$updated)
                                 * Это должно сильно сократить время запроса в БД.
                                 *
                                 * Получим список в котором будут объекты, которые нужно добавить к перечню.
                                 * А если в списке измененных есть старый объект, а в новом списке его нет, то нужно его
                                 * удалить из имеющегося перечня.
                                 */
                                if (isset($sessionLists[$key]['frames'])) {
                                    $arIntersect = array_intersect($sessionLists[$key]['frames'], $updated);
                                    $filterTemp['_buster_updated'] = $updated;
                                    $filterTemp['_buster_lastping'] = $lastPing;
                                } else {
                                    $sessionLists[$key]['frames'] = [];
                                }
                            }
                        }
                    }
                    $currentList = (isset($sessionLists[$key]['frames'])) ? $sessionLists[$key]['frames'] : [];

                    $collection = $class::_getVortexList($vid, $filterTemp, $currentList);
                    //Если есть индивидуальный хэш-генератор, то используем его
                    $func = "{$vid}ListHash";
                    $newHash = ($vid && method_exists($class, $func))? $class::$func($collection): $class::_getVortexListHash($collection);

                    if ($hash == $newHash && $filter == $newFilter) continue;
                    $frames = $collection->lists('id');

                    if ($vortexHashBuster && $arIntersect !== null && isset($sessionLists[$key]) && $filter == $newFilter && !isset($filterTemp['_complete_list'])) {
                        /* Собственно сам момент вставки новых объектов и удаление старых */
                        /* Количество удаляемых объектов при бусте */
                        $remove = 0;
                        foreach ($arIntersect as $intersecItem)
                            if (($k = array_search($intersecItem, $sessionLists[$key]['frames'])) !== false) {
                                unset($sessionLists[$key]['frames'][$k]);
                                $remove += 1;
                            }
                        $newframes = $frames->toArray();
                        $frames = array_merge($sessionLists[$key]['frames'], $newframes);
                        if (count($newframes) == 0 && $remove == 0)
                            $newHash = $hash;

                        /* Переписываем count */
                        $tempFilter = json_decode($filter, true);
                        if (isset($tempFilter['_count'])) {
                            $filterTemp['_count'] = $tempFilter['_count'] - $remove + count($newframes);
                        }
                    } else {
                        if (isset($filterTemp['_complete_list'])) {
                            unset($filterTemp['_complete_list']);
                        }
                    }
                    if (isset($filterTemp['_count'])) $newHash .= $filterTemp['_count'];
                    $frameVID = $class::_getVIDforList($vid);

                    $count = (isset($filterTemp['_count'])) ? $filterTemp['_count'] : -1;
                    $skip = (isset($filterTemp['_skip'])) ? $filterTemp['_skip'] : 0;
                    $take = (isset($filterTemp['_take'])) ? $filterTemp['_take'] : 10;
                    $left = ($count >= 0) ? $count - $take - $skip : -1;
                    $filterTemp['_left'] = $left;
                    $responceLists[$key] = [
                        'id' => $vlist['id'],
                        'vid' => $vlist['vid'],
                        'frames' => $frames,
                        'childVID' => $frameVID,
                        'hash' => $newHash,
                        'left' => ($left < 0) ? 0 : $left,
                        'count' => $count,
                    ];
                    $sessionLists[$key]['hash'] = $newHash;
                    $sessionLists[$key]['checkfilter'] = json_encode($filterTemp);

                    /* Для буста требуется хранить перечень фреймов, что увеличивает требования к памяти */
                    if ($vortexHashBuster) {
                        if (!is_array($frames))
                            $sessionLists[$key]['frames'] = $frames->toArray();
                        else
                            $sessionLists[$key]['frames'] = $frames;
                    }

                    /*
                     * Дополняем перечень используемых фреймов
                     */
                    foreach ($frames as $frame) {
                        $tempVID = "{$arr[0]}.{$frameVID}.ID{$frame}";
                        if (isset($sessionFrames[$tempVID])) continue;
                        $sessionFrames[$tempVID] = [
                            'hash' => '',
                            'id' => $frame,
                            'card' => false,
                            'vid' => "{$arr[0]}.{$frameVID}",
                        ];
                    }

                } catch (\Exception $e) {
                    unset($sessionLists[$key]);
                    return Response::json(['debug' => $e->getMessage() . PHP_EOL . PHP_EOL . 'Trace: ' . PHP_EOL . $e->getTraceAsString()]);
                }
            }
            $sessionsList[$sessionID]['vlists'] = $sessionLists;
            session(['sessions' => $sessionsList]);
            /* Обновляем данные в сессии, при включеном бустере */
            if ($vortexHashBuster) {
                foreach ($newBustList as $key => $value) {
                    $bustList[$key] = $value;
                }
                $sessionsList[$sessionID]['_vortexBustList'] = $bustList;
            }

            foreach ($sessionFrames as $key => $vframe) {
                try {
                    $arr = explode('.', $vframe['vid']);
                    if (count($arr) < 2) {
                        unset($sessionFrames[$key]);
                        continue;
                    }
                    $class = str_replace('_', '\\', $arr[0]);
                    $vid = $arr[1];
                    $hash = (isset($vframe['hash'])) ? $vframe['hash'] : '-1';
                    $id = (isset($vframe['id'])) ? $vframe['id'] : '-1';

                    /** @var Vortex $model */
                    if (!$model = $class::find($id)) {
                        unset($sessionFrames[$key]);
                        $responceFrames[$key] = [
                            'vid' => $vframe['vid'],
                            'id' => $id,
                            'data' => '{}',
                            'hash' => 'wrong',
                            'card' => ''
                        ];
                        continue;
                    };

                    //Если есть индивидуальный хэш-генератор, то используем его
                    $func = "{$vid}Hash";
                    $newHash = ($vid && method_exists($class, $func))? $model->$func(): $model->_getVortexHash();

                    if ($hash == $newHash) continue;
                    $sessionFrames[$key]['hash'] = $newHash;

                    $data = $model->_getVortexData($vid);
                    $card = null;
                    if (isset($vframe['card'])) {
                        $card = $model->_getVortexHTML($vid);
                        unset($sessionFrames[$key]['card']);
                    }
                    $responceFrames[$key] = [
                        'vid' => $vframe['vid'],
                        'id' => $id,
                        'data' => $data,
                        'hash' => $newHash,
                        'card' => $card
                    ];
                    $sessionFrames[$key]['hash'] = $newHash;
                } catch (\Exception $e) {
                    unset($sessionFrames[$key]);
                    return Response::json(['debug' => $e->getMessage() . PHP_EOL . PHP_EOL . 'Trace: ' . PHP_EOL . $e->getTraceAsString()]);
                }
            }
            $sessionsList[$sessionID]['lastPing'] = Carbon::now();
            $sessionsList[$sessionID]['vframes'] = $sessionFrames;
            session(['sessions' => $sessionsList]);

            $responce = [
                'responce' => [
                    'frames' => $responceFrames,
                    'lists' => $responceLists,
                ],
            ];

            if (count($sessionLists) == 0 && count($sessionFrames) == 0) {
                $responce['responce']['empty'] = true;
            }

            /* Отсылаем Дебаг данные, если они есть */
            if (isset($debug))
                $responce['debug'] = $debug;

            return Response::json($responce);
        } catch (\Exception $e) {
            session(['sessions' => []]);
            if (env('APP_DEBUG', false))
                return Response::json(['error' => $e->getMessage() . '<br>' . $e->getFile() . '<br>' . $e->getLine(),
                    'debug' => (is_string($e->getTrace())) ? $e->getTrace() : $responce
                ]);
            else {
                if (Auth::check())
                    file_put_contents('./../storage/logs/vortex_errors.log', Auth::user()->email . PHP_EOL, FILE_APPEND);
                else
                    file_put_contents('./../storage/logs/vortex_errors.log', 'not login' . PHP_EOL, FILE_APPEND);
                file_put_contents('./../storage/logs/vortex_errors.log', $e->getMessage() . PHP_EOL . $e->getFile() . PHP_EOL . $e->getLine() . PHP_EOL, FILE_APPEND);
                foreach ($e->getTrace() as $trace) {
                    if (isset($trace['line']) && isset($trace['file']))
                        file_put_contents('./../storage/logs/vortex_errors.log', $trace['file'] . '   :' . $trace['line'] . '   args:' . json_encode($trace['args']) . PHP_EOL, FILE_APPEND);
                }
                file_put_contents('./../storage/logs/vortex_errors.log', PHP_EOL, FILE_APPEND);
                return Response::json([]);
            }
        }
    }

    public function startVortex()
    {
        $sessionsList = session('sessions', []);
        $sessionID = Input::get('_session');
        $sessionData = ['vlists' => [], 'vframes' => [], '_vortexBustList' => []];
        $sessionsList[$sessionID] = $sessionData;
        session(['sessions' => $sessionsList]);
        return Response::json(['log' => 'Vortex стартовал']);
    }

    public function action()
    {
        /* Структура запроса
         *
         * data=>
         *      model - класс модели
         *      method - название метода
         *      args - аргументы для метода
         */

        $request = Input::get('data');
        $model = null;
        if (isset($request['model'])) {
            $request['model'] = str_replace('.', '\\', ucfirst($request['model']));
            /* Если существует запрошенная модель, то берется она, если нет, то берется с приставкой App\ */
            $model = (class_exists($request['model'])) ? $request['model'] : 'App\\' . ucfirst($request['model']);
        }
        $method = (isset($request['method'])) ? $request['method'] : null;
        $args = (isset($request['args'])) ? $request['args'] : [];

        if (
            $model == null
            || $method == null
            || !class_exists($model)
            || !method_exists($model, $method)
        ) return Response::json(['error' => 'Ошибка в запросе. Модель:' . $model . '. Метод:' . $method]);

        /*
         * $msg - ответ функции - дожен иметь вид ассоциативного массива, где распознаются поля
         *      responce - стандартное хранилище переменных возвращаемых клиенту
         *      error - окно с сообщением об ошибке
         *      debug - запись в лог с маркером DEBUG
         *      log - запись в лог
         *      msg - текстовое сообщение для пользователя ['title','text','btn']
         *      events - функции, вызов которых инициируется на клиенте ['func', 'args']
         */
        try {
            $msg = $model::$method($args);
        } catch (\Exception $e) {
            file_put_contents('./../storage/logs/vortex_errors.log', $e . PHP_EOL, FILE_APPEND);
            $js_args = (isset($request['args'])) ? json_encode($request['args']) : json_encode($request);
            return Response::json([
                'error' => "Ошибка при выполнении запроса. Модель: {$model}\nМетод: {$method}\nПараметры{$js_args}",
                'debug' => $e->getMessage() . PHP_EOL . PHP_EOL . 'Trace: ' . PHP_EOL . $e->getTraceAsString()
            ]);
        }
        return ($msg != null) ?
            Response::json($msg) :
            Response::json([]);
    }
}
