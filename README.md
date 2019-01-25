# Vortex
# Плагин для Laravel, упрощающий создание элементов, обновляющихся по пингу

Laravel's plugin. It simplifies the creation of items that are updated by ping

## Установка:

* Скопировать содержимое папки vortex в папку проекта laravel.
* Добавить руты из \app\Http\vortex_routes.php
* Добавить строку из .env.vortex в ваш .env, если хотите использовать аналитический ускоритель (осторожно! Возможны баги!)
* Если вы используете ускоритель, произведите миграцию в БД (файл \database\migrations\2018_11_30_033655_vortex_change_log.php), для создания таблицы учета изменений
* Подключить скрипт \public\js\servicevortex.js в шапке основного шаблона (или отдельной страницы)

## Понижение версии
Если вы используете Laravel версии 5.1 или ниже, произведите замену ->pluck( на ->lists( в файле \app\Http\Controllers\Vortex\VortexController.php

## Как это работает.
Скрипт будет отправлять на сервер каждые 6секунд запрос для проверки изменений. При наличии изменений, с сервера придет пакет данных, которые будут отражены в вортекс-элементах. Таким образом, можно делать мессенджеры или подобные системы.
Если окно выйдет из фокуса более чем на 5 минут, то при возвращении в фокус произойдет запрос полного списка данных и полная перерисовка вортекс-элементов. Это сделано для обхода блокировки некоторых браузеров (например Хрома) на активность вне фокуса.
Если не проявлять активность в течении 16ти секунд, то интервал между пингами возрастет до 20 секунд.
Эти параметры можно менять, задавая значения переменных:
```js
Vortex.pingDelay: 6000, //Интервал между пингами
Vortex.pingLongDelay: 20000, //Интервал между пингами, а режиме ожидания
Vortex.sleepDelay: 16000, //Сколько времени ждать до ухода в режим ожидания
```

При выводе на экран списков, может возникнуть серьезная задержка, если обрабатываются большие массивы данных. Например запрос на вывод первых 20 документов из имеющихся 10000, отсортированных по заголовку, может составлять 4-6 секунд. Этот отклик останется таким же большим при любом изменении списка, так как производится повторное формирование списка по тем же условиям. Для того чтобы уйти от этой проблемы в Vortex встроен ускоритель. 
При использовании ускорения, скрипт на сервере будет анализировать только те элементы, которые подверглись изменению за время прошедшее с последнего пинга. После чего производится достраивание текущего списка до нового вида и сброс данных на клиент.
Данный подход чреват ошибками и не оттестирован полностью. Будьте осторожны!

Элемент в левом нижнем углу - это индикатор работы контроллера Vortex. Если он мешает его можно убрать через стили указав
```css
#vortex_pulse{display:none}
```

## Как использовать вортекс-элементы.
Чтобы вставить обновляемый ви-фрейм модели (ви-фрейм - это элемент обрабатываемый скриптами плагина), необходимо в классе этой модели указать использование трейта Vortex. 
**ВАЖНО! В Базе Данных у используемой модели обязательно должно присутствовать поле updated_at!**

например, для класса User мы получаем следующий код:

```php
namespace App;

use App\Vortex\Vortex;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;

class User extends Authenticatable
{
    use Notifiable, Vortex;
    
    ...
    
}
```

Теперь необходимо указать название ви-фрейма и шаблон для него.
Пример можно увидеть в самом файле трейта Vortex

```php
namespace App;

use App\Vortex\Vortex;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;

class User extends Authenticatable
{
    use Notifiable, Vortex;
    
    protected static $vframes = [
        "profile" => "user.card"
    ];

    protected static $vlists = [
        "usersIndex" => "profile"
    ];
    
    ...
    
}
```
Здесь мы указали ви-фрейм profile, который использует шаблон resources\view\user\card.blade.php и ви-лист usersIndex, который будет состоять из ви-фреймов profile.

Теперь, нужно создать сам шаблон resources\view\user\card.blade.php.
Например в таком виде:
```php
<div class="card">
    <div class="card-header">
        <span mark="name"></span>
    </div>
    <div class="card-body">
        <span mark="email"></span>
    </div>
</div>
```
Здесь имеются атрибуты mark, которые показывают какой именно параметр модели будет использоваться для заполнения области.
Если мы теперь вставим на какой-либо странице код
```php
{{\App\User::vlist()}}
```
то мы должны получить следующую структуру:
```html
<vlist vid="App_User.">
    <vframe vid="App_User.profile" mid="1">
        <div class="card"> 
            <div class="card-header"> 
                <span mark="name">Tester1</span> 
            </div> 
            <div class="card-body"> 
                <span mark="email">test1@mail.ru</span> 
            </div>
        </div>
    </vframe>
    <vframe vid="App_User.profile" mid="2">
        <div class="card">
            <div class="card-header">
                <span mark="name">Tester2</span>
            </div>
            <div class="card-body">
                <span mark="email">test2@mail.com</span>
            </div>
        </div>
    </vframe>
</vlist>
```
Это получился список из двух ви-фреймов (если в БД занесены только 2 пользователя). При этом каждый ви-фрейм заполнен данными соответствующего пользователя.
Обратите внимание: vid ви-листа выглядит неполным ("App_User."). Это произошло потому что мы не указали название списка 
```php
{{\App\User::vlist()}}
```
В этом случае, будет взят первый из доступных списков модели. Правильнее будет использовать явное указание на тип списка 
```php
{{\App\User::vlist("usersIndex")}}
```
Если нужно вставить ви-фрейм отдельной модели, то это можно сделать следующей строкой:
```php
{{\App\User::find(1)->vframe("profile")}}
```
Здесь мы ищем в БД нужную нам модель (в данном случае по id) и вызываем для нее функцию vframe.

Так же можно производить вставку вортекс-элементов средствами JS.
Например вставка ви-фрейма того-же первого пользователя будет выглядеть так:
```js
Vortex.insertNewFrame('App_User', 1, 'profile', $('body')[0]);
```
Здесь мы указываем класс модели, используя _ вместо \, ID модели в БД, тип ви-фрейма и элемент к которому нужно добавить новый элемент.
Список добавляется аналогично
```js
Vortex.insertNewList('App_User', {'id >' :1}, 'usersIndex', $('body')[0]);
```
Второй параметр в скобках - это фильтр для списка. Если указать просто параметр, это будет эквивалентом ==, если же указать параметр и через пробел оператор, то будет использоваться указанный оператор. Распознаются все операторы Laravel допустимые в качестве параметра для функции where(). Можно, например, использовать конструкцию
```js
Vortex.insertNewList('App_User', {'name like' :'%tes%'}, 'usersIndex', $('body')[0]);
```

Так же в качестве параметров для фильтра принимаются следующие значения:
* _skip - команда пропустить часть результатов при фильтрации
* _take - команда взять не более указанного значения результатов
* _order - сортировать по параметру
* _orderdesc - сортировать по параметру по убыванию

Зарезервированные параметры:
* _buster_lastping
* _buster_updated
* _complete_list
* _left
* _count

Особенностью подсключения вортекс-элементов через JS является то, что пока идет подгрузка данных, на их место добавляется элемент
```html
<div class='loading-img'><img src='" + this.loadingIco + "'></div>
```
соответственно поменять loadingIco можно командой
```js
Vortex.loadingIco = "путь до графического файла"
```

## Обработка выводимых данных
Часто будет требоваться вывести не чистое значение параметра модели, а какое-то обработанное. Самые просто пример - форматирование параметра updated_at в нужную форму.

для этого нужно в классе модели создать функцию "название ви-фрейма"Data().
То есть для нашего примера выше это будет 
```php
    private function profileData()
    {
        $data = new \stdClass();
        $data->name = $this->name;
        $data->updated = $this->updated_at->format('d.m.Y H:i');
        return $data;
    }
```
или вот так, если много параметров и лень их копировать построчно
```php
    private function profileData()
    {
        $data = clone $this;
        $data->updated = $this->updated_at->format('d.m.Y H:i');
        return $data;
    }
```
В файле Vortex есть пример такой функции (exampleData) для копи-паст переноса в модель.

Вторая частая потребность - это более сложный фильтр (или просто фильтр, недоступный с клиента)
Аналогично, в этом случае создаем новую функцию в модели. Но теперь ее название будет "название ви-листа"Filter()
(Пример, опять же, есть в файле Vortex)
```php
    private static function usersIndexFilter(&$filter, $currentList = null)
    {
        $list = static::where('updated_at', '>', Carbon::now()->addDays(-5)); //Например вот такое вот ограничение
        static::simpleFilter($list, $filter);
        $list = $list->get();
        return $list;
    }
```
Здесь сложнее.
$filter - хранит данные по фильтрации со стороны клиента.
$currentList - содержит текущий перечень ID в списке.
Так же здесь вы видите функцию simpleFilter.
Эта функция прогоняет стандартные процедуры фильтрации, включая skip и take. И здесь появляется нюанс! Если вы используете ускорение, а фильтр для списка у вас сложнее, чем приведенный пример "static::where('updated_at', '>', Carbon::now()->addDays(-5))", а главное, фильтр динамический, то потребуется использовать запрет на ускорение внутри simpleFilter, указав третьим параметром true.
Получим вот такой вариант:
```php
    private static function usersIndexFilter(&$filter, $currentList = null)
    {
        $list = static::dinamicFilterSystem(); //Сложный, динамический фильтр
        static::simpleFilter($list, $filter, true);
        $list = $list->get();
        return $list;
    }
```
Часто, в этом не будет необходимости и проще будет произвести фильтрацию через клиент или модификацией переменной $filter. Но, тем не менее, когда это будет не доступно, можно делать как в указанном примере.

## Дополнительный функционал
### DEBUG мод
Если вы используете Vortex, то нажатие Ctrl+Alt+Num0 переведет его в Дебаг-режим.
При этом в консоли браузера будет выводиться более подробная информация, а на всех видимых вортекс-элементах появятся кнопки. Если использовать такую кнопку, то будет произведен вывод данных по данному элементу в консоли браузера, а сам элемент будет сохранен в переменной vTemp.
Таким образом можно смотреть текущие параметры вортекс-элементов и вызывать доступные функции.

### Функции
* vPing() - опрос сервера на наличие изменений.
* Vortex.recheck() - Функция переводит все списки и фреймы в положение "неподтвержден". Если списков и фреймов нет, то возвращает false. Если возвращается true, то есть данные для запроса от сервера и после вызова recheck() требуется вызов vPing()
* Vortex.insertNewFrame(model, id, vid, parent = document.getElementById('app'), wait = false) - Функция добавления фрейма для подгрузки с сервера model - класс модели-родителя. Например App.User (. или _ используется как разделитель вместо \ ), id - ID модели в БД, vid - ключ типа vframes. Должен присутствовать в массиве $vframes модели родителя, parent - элемент, к котрому прикрепится новый фрейм, wait - задержка пинга. Если не указана, то считается false, возвращает DOM Элемент
* Vortex.insertNewList(model, filter, vid, parent = document.getElementById('app'), wait = false, isAccumulator = true) - Добавляем новый список, model Класс, filter Фильтр, vid Тип списка (согласно protected static $vlists класса), parent Родитель в DOM структуре, wait Ожидание. Если ожидание стоит false или не указано, то сразу после вставки списка пройдет команда vPing(), возвращает DOM Элемент-список
* Vortex.setFilter(listVID, listID, filter) - задать новый фильтр для ви-листа.
* Vortex.action(model, method, args = {}) - Активация функции модели. model - класс модели (_ вместо \), method - название static функции, args - список аргументов для функции.
* Vortex.searchList(element) -  Поиск элемента vlist в архиве Vortex, соответствуюшего DOM элементу. Если передан массив jQuery (имеется параметр length >0) то работаем с нулевым элементом
* Vortex.searchFrame(element) - Поиск элемента vframe в архиве Vortex, содержащего указатель на DOM элемент

vlist - элемент полученный либо через использование DEBUG режима в переменную vTemp, либо через Vortex.searchList().
* vlist.setFilter(filter, wait = false) - Установка нового фильтра. Если wait != false в конце будет вызов vPing().
* vlist.sort(newOrder, newOrderDesc = false) - Сортировка списка. newOrder - параметр, по которому ведется сортировка, newOrderDesc == true - сортировка по уменьшению. Перед началом сортировки вызывается событие $(document).trigger('before_list_sort'), после сортировки $(document).trigger('after_list_sort').
* vlist.copyFrameToList(destination, idFrame) - Функция копирования фрейма из одного списка в другой. Если idFrame не задан, то переноситься будут все фреймы.
* vlist.refill(wait = true) - Функция очищает текущее наполнение списка, выставляет checked = false и hash = ''. HTML код отрисовывает иконку загрузки. Если при этом не задана wait=true, то происходит репинг на сервер.

## Switcher
Свитчер - это элемент-переключатель с двумя или более рабочими состояниями. Можно его назвать интерактивным аналогом checkbox.
Свитчер всегда находится в одном из своих состояний, при входе в новое состояние, произойдет вызов указанных в коде событий.

Пример использования:
```php
    <div switcher="set_mode" class="btn" set state="active" group="tools">
        <state set="">
            <event>alert('passive')</event>
            PASSIVE
        </state>
        <state set="active">
            <event>alert('active')</event>
            ACTIVE
        </state>
    </div>
```
Разберем детально.
switcher="set_mode" - это указание скрипту, что данный элемент является переключаемым и заносит его в реестр с идентификатором set_mode
Если у вас будет два одинаковых переключателя с одним ID, то при переключении одного из них, второй так же поменяет сове положение. При этом скрипт будет выполнен только один раз.
set - этот параметр нужно указывать, если переключение происходит при клике по переключателю. Если этого параметра нет, то потребуется добавлять органы управления, которые будут переводить переключатель в новое положение.
state="active" - состояние переключателя на момент загрузки страницы. Если одинаковых переключателей несколько, то кто первый загрузился, тот дефолтное значение и указывает. Если state не указан - берется state="".
group="tools" - указание группы переключателей. Может быть не указано. Если переключатель относится к группе, то при его переключении в любое, отличное от "" состояние, все остальные переключатели данной группы вернутся в состояние "" с выполнением скриптов. Скрипты будут выполнены, даже если предыдущее состояние переключателя было "".

Далее идет роспись состояний.
<state set=""> - состояние ""
    <event>alert('passive')</event> - событие при активации состояния
    HTML код, который будет наполнять элемент в этом состоянии
<\state>
    
для переключателя придусмотрена одна функция: 
switcher.set(newState, withEvent = true) - переключение в указанное состояние. Если newState не задан, то происходит псевдо-переключение в текущее состояние, без выполнения скриптов. withEvent - выполнение скриптов, если FALSE, то будет только перерисовка переключателя.
