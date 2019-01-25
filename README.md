# Vortex
h1 Плагин для Laravel, упрощающий создание элементов, обновляющихся по пингу

Laravel's plugin. It simplifies the creation of items that are updated by ping

## Установка:

* Скопировать содержимое папки vortex в папку проекта laravel.
* Добавить строку из .env.vortex в ваш .env, если хотите использовать аналитический ускоритель (осторожно! Возможны баги!)
* Если вы используете ускоритель, произведите миграцию в БД (файл \database\migrations\2018_11_30_033655_vortex_change_log.php), для создания таблицы учета изменений
* Подключить скрипт \public\js\servicevortex.js в шапке основного шаблона (или отдельной страницы)

## Как это работает.

Чтобы вставить обновляемый ви-фрейм модели (ви-фрейм - это элемент обрабатываемый скриптами плагина), необходимо в классе этой модели указать использование трейта Vortex.

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
