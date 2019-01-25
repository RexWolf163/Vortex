# Vortex
Плагин для Laravel, упрощающий создание элементов, обновляющихся по пингу

Laravel's plugin. It simplifies the creation of items that are updated by ping

Установка:
* Скопировать содержимое папки vortex в папку проекта laravel.
* Добавить строку из .env.vortex в ваш .env, если хотите использовать аналитический ускоритель (осторожно! Возможны баги!)
* Если вы используете ускоритель, произведите миграцию в БД (файл database\migrations\2018_11_30_033655_vortex_change_log.php), для создания таблицы учета изменений

Как это работает.

Чтобы вставить обновляемый фрейм модели, необходимо в классе этой модели указать использование трейта Vortex.

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
