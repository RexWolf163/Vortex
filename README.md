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
