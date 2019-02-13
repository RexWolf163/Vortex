<?php
/**
 * Created by PhpStorm.
 * User: Wolf
 * Date: 20.11.2018
 * Time: 4:20
 */

namespace App\Vortex;


use App\Http\Controllers\Service\ServiceController;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\DB;

trait Vortex
{
    /**
     * Список "vID фрейма" => "view элемент"
     * @var array
     */
//    protected static $vframes = ['indexcard' => 'user.card'];

    /**
     * Список "vID списка" => "vID фрейма элементов в списке"
     * @var array
     */
//    protected static $vlists = ['index' => 'indexcard'];

    /**
     * Пример функции, которая готовит пакет для заполнения меток.
     *
     * @return JSON mixed закодированный в json пакет данных для заполнения меток
     */
    private function exampleData()
    {
        $data = new \stdClass();
        $data->name = 'test';
        return $data;
    }

    /**
     * Пример функции-фильтра данных для списка.
     *
     * @var array $filter настройки фильтра
     * @var array $currentList Текущее состояние списка (опционально)
     *
     * @return Collection Коллекция моделей, для отображения в списке. Передает из сессии VortexController.
     */
    private static function exampleFilter(&$filter, $currentList = null)
    {
        $list = static::where('id', '>', 0);
        static::simpleFilter($list, $filter);
        $list = $list->get();
        return $list;
    }

    /**
     * Пример функции-формировщика Хэша.
     * Этот формирователь хэша будет вызван вместо стандартной функции.
     * example - заменить на ключ ви-фрейма
     * Применяется для фреймов выпадающих из общих правил.
     * @return string
     */
    public function exampleHash()
    {
        $base = '1';
        return $base;
    }

    /**
     * Пример функции-формировщика Хэша.
     * Этот формирователь хэша будет вызван вместо стандартной функции.
     * exampleList - заменить на ключ ви-списка
     * Применяется для фреймов выпадающих из общих правил.
     * @return string
     */
    public static function exampleListHash($list)
    {
        $base = '1';
        return $base;
    }

    /**
     * Формируем HTML
     * @return string
     */
    public function _getVortexHTML($vid)
    {
        $view = (isset(static::$vframes[$vid])) ? static::$vframes[$vid] : null;
        if (!$view) return "";
        $html = preg_replace('/\s\s+/', ' ', view($view, ['item' => $this])->render());
        return $html;
    }

    /**
     * Формируем Хэш
     * @return string
     */
    public function _getVortexHash()
    {
        $base = (isset($this->updated_at)) ? ServiceController::alfaEncode($this->updated_at->format('YmdHis')) : '1';
        return $base;
    }

    /**
     * Формируем Хэш
     * @return string
     */
    public static function _getVortexListHash($list)
    {
        $t = $list->max('updated_at');
        if (!$t) return '1';
        $base = (!is_string($t)) ? ServiceController::alfaEncode($t->format('YmdHis')) : 0;
        return $base;
    }

    /**
     * Фильтруем список
     * @return string
     */
    public static function _getVortexList($vlistID, &$filter, $currentList = null)
    {
        $func = "{$vlistID}Filter";
        if ($vlistID && method_exists(get_called_class(), $func)) {
            /** @var Collection $list */
            $list = static::$func($filter, $currentList);
        } else {
            $list = static::where('id', '>', 0);
            static::simpleFilter($list, $filter);
            $list = $list->get();
        }

        return $list;
    }

    /**
     * Собираем данные для отображения
     * @param $vid
     * @return mixed
     */
    public function _getVortexData($vid)
    {
        $func = "{$vid}Data";
        $data = (method_exists($this, $func)) ? $this->$func() : $this;
        return str_replace('+', '%20', urlencode(json_encode($data)));
    }

    /**
     * Выбираем идентификатор для фрейма внутри списка по имени списка
     * @param $vlistID
     * @return mixed
     */
    public static function _getVIDforList($vlistID = '')
    {
        /*Название шаблона берется из массива $vlists. Если указанного ключа не существует, то берется
         * указанный ключ в массиве $vframes. Если его так же не существует, то берется первый ключ массива $vframes.
         * Иначе выдается ошибка.
         */
        if (isset(static::$vlists[$vlistID])) {
            $vid = static::$vlists[$vlistID];
        } elseif (isset(static::$vframes[$vlistID])) {
            $vid = static::$vframes[$vlistID];
        } elseif (count(static::$vframes) > 0) {
            $tempArr = array_keys(static::$vframes);
            $vid = reset($tempArr);
        } else return '';
        return $vid;
    }

    /**
     * Формирование списка с объединителем
     * Список с объединителем не показывает фреймы по отдельности, он сцепляет их содержимое и выводит их внутри помеченного
     * элемента объединителя - $combiner. Элемент маркируется атрибутом mark
     *
     * @param string $vlistID Название функции модели для формирования списка. Функция должна называться
     *                          "{$vlistID}Filter". Если функция не указана, то берется полная выборка из
     *                          БД по данной модели с учетом фильтра
     * @param array $filter Правила фильтрации для списка. Может быть не указано. Если не указан ни фильтр ни список,
     *                          то это эквивалентно выборке всех элементов по нисходящей от времени изменения.
     * @param boolean $accumulator Если TRUE, то списко накопительный, то есть все ранее обавленные фреймы на уровне
     *                          интерфейса будут сохраняться, даже если при новой проверке их уже не должно быть.
     * @param boolean $include Если FALSE, то Html код будет добавлен командой echo, иначе будет возвращена строка с кодом
     * @param boolean $freeze "Замороженные" списки не запрашивают свое состояние с сервера.
     * @return null||string
     */
    public static function vcombiner($vlistID = "", $filter = [], $combiner = "<div></div>", $accumulator = false, $include = false, $freeze = false)
    {
        $list = static::vlist($vlistID, $filter, $accumulator, true, $freeze);
        //производим подмену обозначения html элемента
        $list = '<vlist combiner ' . substr($list, 7);
        $list = substr($list, 0, -8) . '<combiner>' . $combiner . '</combiner></vlist>';
        if (!$include)
            echo $list;
        else return $list;
        return null;
    }

    /**
     * Формирование списка
     *
     * @param string $vlistID Название функции модели для формирования списка. Функция должна называться
     *                          "{$vlistID}Filter". Если функция не указана, то берется полная выборка из
     *                          БД по данной модели с учетом фильтра
     * @param array $filter Правила фильтрации для списка. Может быть не указано. Если не указан ни фильтр ни список,
     *                          то это эквивалентно выборке всех элементов по нисходящей от времени изменения.
     * @param boolean $accumulator Если TRUE, то списко накопительный, то есть все ранее обавленные фреймы на уровне
     *                          интерфейса будут сохраняться, даже если при новой проверке их уже не должно быть.
     * @param boolean $include Если FALSE, то Html код будет добавлен командой echo, иначе будет возвращена строка с кодом
     * @param boolean $freeze "Замороженные" списки не запрашивают свое состояние с сервера.
     * @return null||string
     */
    public static function vlist($vlistID = "", $filter = [], $accumulator = false, $include = false, $freeze = false)
    {
        $freezeFlag = ($freeze) ? "freeze " : "";
        $list = ($freeze) ? collect([]) : static::_getVortexList($vlistID, $filter);
        if($freeze){
            $hash = "-1";
        }else{
            $hash = (method_exists(static::class, $func))?static::$func($list): static::_getVortexListHash($list)
        }
        $class = str_replace('\\', '_', static::class);

        $vid = static::_getVIDforList($vlistID);

        $jsonFilter = json_encode($filter);
        if (!$vlistID) $vlistID = '';
        $page = ($accumulator) ? '' : ' page';
        $vlist = [];
        $vlist[] = "<vlist {$freezeFlag}vid='{$class}.{$vlistID}'{$page}><filter>{$jsonFilter}</filter><hash>{$hash}</hash>";

        /** @var static $item */
        foreach ($list as $item) {
            $vlist [] = $item->vframe($vid, true);
        }
        $vlist [] = "</vlist>";

        $vlist = implode('', $vlist);
        if (!$include)
            echo $vlist;
        else return $vlist;
        return null;
    }

    public function incVframe($vid)
    {
        $this->vframe($vid, true);
    }

    /**
     * отрисовка pingframe
     * @param string $vid ID view элемента в массиве $vframes
     * @param boolean $include Если vframe вызывается как вложенный элемент другого vortex-элемента,
     *      то следует поставить true, в этом случае функция вернет строку, иначе произойдет вызов команды echo
     * @return string||null
     */
    public function vframe($vid, $include = false)
    {
        $view = (isset(static::$vframes[$vid])) ? static::$vframes[$vid] : null;
        if (!$view) return null;
        $html = $this->_getVortexHTML($vid);
        $func = "{$vid}Hash";
        $hash = (method_exists(static::class, $func))?$this->$func(): $this->_getVortexHash();
        $class = str_replace('\\', '_', static::class);
        $data = $this->_getVortexData($vid);
        $id = $this->id;
        $vframe = "<vframe vid='{$class}.{$vid}' mid='{$id}'><id>{$id}</id><hash>{$hash}</hash><data>{$data}</data>{$html}</vframe>";

        //Добавляем запись в Сессию
        $sessionFrames = session('vframes', []);
        $sessionFrames["{$class}.{$vid}.ID{$this->id}"] = [
            'hash' => $hash,
            'id' => $id,
            'vid' => "{$class}.{$vid}"
        ];
        session(['vframes' => $sessionFrames]);

        if (!$include)
            echo $vframe;
        else return $vframe;
        return null;
    }

    public static function getVFrame($id, $vid, $include = false)
    {
        if (!$item = static::find($id)) return '';
        return $item->vframe($vid, $include);
    }

    /**
     * @param Collection $list - предварительный список для обработки
     * @param array $filter - фильтр
     * @param bool $refill - Если TRUE, значит идет перезаполнение и отключается ускоритель
     */
    private static function simpleFilter(&$list, &$filter, $refill = false)
    {
        if ($refill) {
            unset($filter['_buster_lastping']);
            unset($filter['_buster_updated']);
        }
        foreach ($filter as $param => $value) {
            if ($param == "_complete_list") continue;
            if ($param == "_skip") continue;
            if ($param == "_take") continue;
            if ($param == "_left") continue;
            if ($param == "_count") continue;
            if ($param == "_buster_updated") continue;
            if ($param == "_buster_lastping") continue;
            if ($param == "_order") {
                $list = $list->orderBy(preg_replace('/[\s-:]/', '', $value));
                continue;
            }
            if ($param == "_orderdesc") {
                $list = $list->orderBy(preg_replace('/[\s-:]/', '', $value), 'desc');
                continue;
            }

            /*
             * Имеется возможность указать сложный фильтр (>=, !=  и т.д.) путем добавления оператора
             * после названия параметра через пробел. Но по умолчанию оператор принимает значение '='
             */
            $operand = '=';
            $tempArr = explode(' ', $param);
            if (count($tempArr) == 2) {
                $param = $tempArr[0];
                $operand = $tempArr[1];
            }
            $list = $list->where($param, $operand, $value);
        }

        $needTake = false;

        //Берем количество позиций до take но после skip
        if (isset($filter['_buster_updated'])) {
            //Добавочный фильтр по последней дате изменения (дописывается при включенном бустере)
            $list = $list->whereIn('id', $filter['_buster_updated']);
        }
        $count = $list->count();
        $skip = 0;
        if (isset($filter['_skip'])) {
            $list = $list->skip($filter['_skip']);
            $needTake = true;
            $skip = $filter['_skip'];
        }
        $take = $count - $skip;
        if (isset($filter['_take'])) {
            $take = $filter['_take'];
            $list = $list->take($filter['_take']);
            $needTake = false;
        }
        if ($needTake) $list = $list->take($take);
        $filter['_count'] = $count;
        $filter['_left'] = $count - $skip - $take;
        if ($filter['_left'] < 0) $filter['_left'] = 0;

        if ($refill) $filter['_complete_list'] = true;
    }

    /**
     * Дописывание метода ларавел для регистрации последнего изменения таблицы модели
     * @param array $options
     * @return mixed
     */
    public function save(array $options = [])
    {
        $saved = parent::save($options);
        if (!env('VORTEX_HASH_BUST', false)) return $saved;
        $table = DB::table('vortex_change_store');
        if ($table->where('table', static::class)->count() == 0) {
            $table->insert(['table' => static::class, 'updated_at' => $this->updated_at]);
            return $saved;
        }
        $table->where('table', static::class)->update(['updated_at' => $this->updated_at]);
        return $saved;
    }
}
