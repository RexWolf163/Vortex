/**
 * Created by Rex Wolf on 20.11.2018.
 *
 * Скрипт для обслуживания vframe и vlist.
 * Идеология Vortex:
 *      Фреймы одинакового типа(vid) обладают одинаковым html представлением(card) с разными текстовыми параметрами.
 *      Одинаковые фреймы - одинаковы. Значит если такой фрейм был добавлен ранее, то новый нужно просто скопировать.
 */

/*
 * Служебные функции.
 */

//Логарифм по основанию
Math.logb = function (number, base) {
    return Math.log(number) / Math.log(base);
};

//Скроллер к элементу
window.scrollToElement = function (theElement, onlyVert) {
    if (onlyVert == undefined) onlyVert = true;
    var selectedPosX = 0;
    var selectedPosY = 0;

    while (theElement != null) {
        if (!onlyVert) selectedPosX += theElement.offsetLeft;
        selectedPosY += theElement.offsetTop;
        theElement = theElement.offsetParent;
    }

    window.scrollTo(selectedPosX, selectedPosY);
};

//Шифратор
var serviceController = {};
serviceController.alfa_codex = '0123456789qwertyuiopasdfghjklzxcvbnmQWERTYUIOPASDFGHJKLZXCVBNM_.,';

serviceController.alfaEncode = function (number) {
    if (number == 0 || number == undefined) return "0";
    var max = serviceController.alfa_codex.length;
    var alfaNumber = '';
    var temp = Math.floor(number / max);
    if (temp != 0 && temp != undefined) {
        alfaNumber = serviceController.alfaEncode(temp);
    }
    alfaNumber += serviceController.alfa_codex[number - (max * temp)];
    return alfaNumber;
};

// возвращает cookie с именем name, если есть, если нет, то undefined
serviceController.getCookie = function (name) {
    var matches = document.cookie.match(new RegExp(
        "(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
    ));
    return matches ? decodeURIComponent(matches[1]) : undefined;
};

serviceController.setCookie = function (name, value, options) {
    options = options || {};

    var expires = options.expires;

    if (typeof expires == "number" && expires) {
        var d = new Date();
        d.setTime(d.getTime() + expires * 1000);
        expires = options.expires = d;
    }
    if (expires && expires.toUTCString) {
        options.expires = expires.toUTCString();
    }

    value = encodeURIComponent(value);

    var updatedCookie = name + "=" + value;

    for (var propName in options) {
        updatedCookie += "; " + propName;
        var propValue = options[propName];
        if (propValue !== true) {
            updatedCookie += "=" + propValue;
        }
    }

    document.cookie = updatedCookie;
};

/*
 Форматирование в финансовый формат
 c - Кол-во знаков после запятой
 d - Тип разделителя целой-дробной
 t - Тип разделителя тысяч

 Спасибо Patrick Desjardins
 */

Number.prototype.formatMoney = function (c, d, t) {
    var n = this,
        c = isNaN(c = Math.abs(c)) ? 2 : c,
        d = d == undefined ? "." : d,
        t = t == undefined ? " " : t,
        s = n < 0 ? "-" : "",
        i = String(parseInt(n = Math.abs(Number(n) || 0).toFixed(c))),
        j = (j = i.length) > 3 ? j % 3 : 0;
    return s + (j ? i.substr(0, j) + t : "") + i.substr(j).replace(/(\d{3})(?=\d)/g, "$1" + t) + (c ? d + Math.abs(n - i).toFixed(c).slice(2) : "");
};

//Проверка, является ли числом
function isNumeric(n) {
    return !isNaN(parseFloat(n)) && isFinite(n);
}

//Модификаци jQuery, чтобы добавить событие на append
(function ($) {
    var origAppend = $.fn.append;

    $.fn.append = function () {
        return origAppend.apply(this, arguments).trigger("append");
    };
})(jQuery);

//Модальник
serviceController.addModal = function (modal, coords) {
    if (coords == undefined) coords = [];
    var content = '<div class="content">' + modal.content + '</div>';
    var id = modal.id;
    if (id != undefined) $('.ui_modal#' + id).trigger('closeModal');
    var title = (modal.title) ? '<h5>' + modal.title + '</h5>' : '';

    var callback = modal.onContentReady;

    var closebtn = (modal.closeBtn !== false) ? '<a class="close_modal"></a>' : '';
    //добавляем кнопки
    var buttons;
    var btnsCallback = [];
    var keys = [];
    if (modal.buttons) {
        buttons = ['<div class="ui_modal_btn_panel">'];
        $.each(modal.buttons, function (i, btn) {
            var btnClasses = (btn.btnClass) ? ' ' + btn.btnClass : '';
            var btnText = btn.text || i;
            buttons.push('<a class="btn' + btnClasses + '">' + btnText + '</a>');
            btnsCallback.push(btn.action);
        });
        buttons.push('</div>');
        buttons = buttons.join('');
    }

    if (buttons === undefined) buttons = '';
    var $modal = '<div class="ui_modal ' + (modal.addClass || '') + '">' + title + closebtn + content + buttons + '</div>';
    var $body = $('body');
    var $shield;
    if (!modal.unshielded) {
        $body.append('<div class="ui_modal_shield"></div>');
        $shield = $('.ui_modal_shield:last');
        $shield.click(function () {
            if ($shield.hasClass('shake')) return;
            $shield.addClass('shake');
            $modal.addClass('shake');
            setTimeout(function () {
                $modal.removeClass('shake');
                $shield.removeClass('shake');
            }, 900)
        });
    }
    $body.append($modal);
    $modal = $('.ui_modal:last').attr('id', id).bind('closeModal', function () {
        $modal.css('opacity', 0);
        if ($shield)$shield.remove();
        setTimeout(function () {
            $modal.remove();
        }, 300);
    }).on('.content', 'append', function () {
        popupController.modalPosition($modal);
    });

    var $content = $modal.find('.content');
    if (typeof callback === "function") callback.call({
        $modal: $modal, $content: $content, close: function () {
            $modal.trigger('closeModal')
        }
    });
    if (modal.draggable)
        $modal.draggable({containment: "parent"}).css('opacity', 1)
            .css('transition', 'opacity ease-in-out 0.2s')
            .css('-o-transition', 'opacity ease-in-out 0.2s')
            .css('-webkit-transition', 'opacity ease-in-out 0.2s');
    else $modal.addClass("frame_container").bind('reframe', function () {
        popupController.modalPosition($modal);
    });

    $.each(btnsCallback, function (i, el) {
        $modal.find('.ui_modal_btn_panel .btn:nth-child(' + (i + 1) + ')').click(function () {
            if (el != undefined)
                el.call({
                    $modal: $modal, $content: $content, close: function () {
                        $modal.trigger('closeModal')
                    }
                });
            else $modal.trigger('closeModal');
        })
    });

    $modal.find('a.close_modal').append('<i class="fas fa-times"></i>').click(function () {
        $modal.trigger('closeModal');
    });
    if (coords.length > 0) {
        var h = $modal.outerHeight();
        var w = $modal.outerWidth();
        var temp;
        temp = Math.floor(coords[0] - w / 2);
        if (temp < 0) temp = 0;
        $modal.css('left', temp);
        temp = Math.floor(coords[1] - h / 2);
        if (temp < 0) temp = 0;
        $modal.css('top', temp);
    }
    else if (popupController) {
        popupController.modalPosition($modal);
    } else {
        location.reload();
    }
    $modal.css('opacity', 1);
};

//popup - окошки
var popupController = {
    delay: 300,
    delayProcess: undefined
};
$(document).ready(function () {
    $('body').append('<div class="popup" id="popup_modal">Главная страница</div>')
        .on('mouseenter', '.popup-container', function () {
            popupController.calcPopupPosition($(this));
            if (popupController.delayProcess != undefined) clearTimeout(popupController.delayProcess);
        }).on('mouseleave', '.popup-container', function () {
        popupController.delayProcess = setTimeout(function () {
            popupController.modal.removeClass('show')
        }, popupController.delay);
    }).on('mouseenter', '#popup_modal', function () {
        popupController.modal.addClass('show');
    }).on('mouseleave', '#popup_modal', function () {
        popupController.modal.removeClass('show');
    });

    popupController.modal = $('#popup_modal');
});

popupController.calcPopupPosition = function (that) {
    var $app = $('#app').get(0).getBoundingClientRect();
    var rect = that.get(0).getBoundingClientRect();
    var comment = that.find('.popup:first');
    if (comment.length == 0) return;
    this.modal.html(comment.html().replace(/\s\s+/g, ' '));
    var h = this.modal.outerHeight();
    var w = this.modal.outerWidth();
    var temp;
    temp = Math.floor(rect.left + rect.width / 2 - w / 2);
    if (temp + w > $app.right) {
        this.modal.css('right', window.innerWidth - $app.right).css('left', '');
    }
    else {
        if (temp < $app.left) temp = $app.left;
        this.modal.css('left', temp).css('right', '');
    }
    if (rect.top < 10)
        temp = Math.floor(rect.bottom) + 5;
    else
        temp = Math.floor(rect.top) - h - 5;
    if (temp < 0) temp = 0;
    this.modal.css('top', temp);
    if (comment.html() != '')
        this.modal.removeClass().addClass(comment.attr('class')).addClass('show');
};

// Позиционирование popup-окошка
popupController.modalPosition = function (that) {
    var $app = $('body').get(0).getBoundingClientRect();
    var modal = that;
    var h = modal.outerHeight();
    var w = modal.outerWidth();
    var temp;
    temp = Math.floor($app.width / 2 - w / 2);
    modal.css('left', temp);
    temp = Math.floor(window.innerHeight / 2 - h / 2);
    modal.css('top', temp);
};

/** vframe обновляется при изменении данных в родительской модели а БД на сервере
 *
 * структура и функционирование vframe:
 *      При загрузке страницы командой $model->vframe($vid) формируется элемент <vframe>
 *      <vframe vid="Класс модели.Класс фрейма">
 *          <id></id>
 *          <hash>
 *              hash код состояния элемента. По стандарту, хэш код формируется альфа-преобразованием
 *              поля updated_at
 *          </hash>
 *          <data>
 *              данные для первичного заполнения в JSON виде
 *          </data>
 *          html код с внедренными маркерами <span mark="ID метки"></span>
 *      </vframe>
 *
 *      После загрузки страницы скрипт формирует в памяти структуру вифреймов (Vortex.vframes), куда выдергивает
 *      hash, id, data, затирая эти поля из html кода, и vid параметр.
 *
 *      Каждые Х секунд производится пинг на сервер. При этом отправляются все хэш ключи в связке с vid
 *      и общее число vframes в памяти vortex
 *
 *      Отпингованые ключи не пингуются до первого изменения. На сервере принятые ключи хранятся в сессии пользователя.
 *
 *      Сервер отвечает на пинг пакетом из измененных хэшей с массивами данных для отметок
 *      и параметр _state, который содержит (при необходимости) новый класс, присвоенный vframe
 *
 *      порядок фрейма задается параметром _order в параметре data
 */

/**  vlist содержит список vframe, который модифицируется (дополняется или сдвигается) при изменениях в БД
 *
 * структура и функционирование vlist (список vframes):
 *      При загрузке страницы командой $model->vlist($filter) формируется элемент <vlist>
 *      <vlist vid="Класс модели.Класс списка">
 *          <filter></filter>
 *          <hash>
 *              hash код состояния элемента. По стандарту, хэш код формируется альфа-преобразованием
 *              максимального значения среди полей updated_at vframes выведенных списком.
 *              При условии неизменности фильтра, это достаточная информация, чтобы оценить неизменность состояния списка
 *          </hash>
 *          наполнение при помощи vframe
 *      </vlist>
 *
 *      После загрузки страницы скрипт формирует в памяти структуру висписков (Vortex.vlists), куда выдергивает
 *      hash, filter, затирая эти поля из html кода, и vid параметр.
 *
 *      Каждые Х секунд производится пинг на сервер. При этом отправляются все хэш ключи в связке с vid
 *      и общее число vlists в памяти vortex
 *
 *      Отпингованые ключи не пингуются до первого изменения. На сервере принятые ключи хранятся в сессии пользователя.
 *
 *      Сервер отвечает на пинг пакетом из измененных хэшей с массивами данных для vlists
 *
 *      Список может быть акумуляторным и страничным. Первый сохраняет все попавшие фреймы дополняя себя новыми,
 *      второй же при каждом изменении чистит себя от "устаревших" фреймов.
 *      При первичном получении страницы с сервера этот тип задается при формировании списка
 *      параметром page для страничных списков
 *
 *      В Vortex хранится указание на каждый элемент с ключом по id модели
 */

/**
 * switches содержит все зарегистрированные переключатели.
 * Переключатель - это несложный элемент интерфейса, призваный выполнять некие действия, одновременно
 * отображая свое состояние.
 * При создании переключателей, не стоит сильно перегружать их вложенными структурами, чтобы избегать "провисаний"
 * скрипта на обработку.
 *
 * Управление элементами switches позволяет синхронное изменение состояний для одноименных переключателей
 * расположенных в разных местах интерфейса
 *
 * Переключатели регистрируются при загрузке страницы и при парсинге ответа сервера на vPing
 * TODO сделать функцию интеграции переключателя через функцию
 *
 * Параметр parent содержит ссылку на vframe. После каждого пинга происходит сличение текущего положения
 * переключателя и состояния родителя. При отклонениях, производится корректировка.
 */

var Vortex = {
    sessionID: Date.now(),
    pingDelay: 6000, //Интервал между пингами
    pingLongDelay: 20000, //Интервал между пингами, а режиме ожидания
    sleepDelay: 16000, //Сколько времени ждать до ухода в режим ожидания
    switcherFreezeDelay: 500, //Подморозка переключателя после клика на него
    frames: {},
    lists: {},
    switches: {},
    switchesGroups: {}, //Группы переключателей
    loadingIco: "/img/v_loading.gif",
    ico: {},
    _premade: {},
    _lastResponce: null, //Хранилище последнего ответа на vPing()
    debug: {
        mode: false,
        _btnListTemplate: '<a class="vortex_scan_btn btn"><img src="/img/vortex_scan.svg"><div>To Console</div></a>',
        _btnFrameTemplate: '<a class="vortex_scan_btn btn"><img src="/img/vortex_scan.svg"></a>',
        _btnSwitcherTemplate: '<a class="vortex_scan_btn btn"><img src="/img/vortex_scan.svg"></a>'
    },

    _showLoading: function () {
        return "<div class='loading-img'><img src='" + this.loadingIco + "'></div>"
    },
    /*
     * Функция переводит все списки и фреймы в положение "неподтвержден".
     * Если списков и фреймов нет, то возвращает false.
     * Если возвращается true, то есть данные для запроса от сервера и после вызова recheck() требуется вызов vPing()
     */
    recheck: function () {
        if (Object.keys(Vortex.lists).length != 0 || Object.keys(Vortex.frames).length != 0) {
            var temp, temp2;
            for (temp in Vortex.frames) {
                if (Vortex.frames.hasOwnProperty(temp)) {
                    for (temp2 in Vortex.frames[temp]) {
                        if (Vortex.frames[temp].hasOwnProperty(temp2)) {
                            Vortex.frames[temp][temp2].checked = false;
                        }
                    }
                }
            }
            for (temp in Vortex.lists) {
                if (Vortex.lists.hasOwnProperty(temp)) {
                    for (temp2 in Vortex.lists[temp]) {
                        if (Vortex.lists[temp].hasOwnProperty(temp2)) {
                            Vortex.lists[temp][temp2].checked = false;
                        }
                    }
                }
            }
            return true;
        }
        return false;
    },
    /**
     * Функция вызывает функцию с сервера и выводит ответ как html код внутри элемента parent
     */
    render: function (model, method, parent, args) {
        if (args == undefined) args = {};
        try {
            parent.html(Vortex._showLoading());
        } catch (e) {
            parent.innerHTML = Vortex._showLoading();
        }
        Vortex.send({'model': model, 'method': method, 'args': args},
            function (data) {
                try {
                    parent.html(data.html);
                } catch (e) {
                    parent.innerHTML = data.html;
                }
                $(document).trigger('vrender_complete');
            },
            'v_action'
        )
    },
    _counter: 0,
    _lostFocusTime: 0
};

Vortex._premade = {};
Vortex._premade.newVlist = function (that, vid, isAccumulator) {
    if (isAccumulator == undefined)isAccumulator = true;
    return {
        vType: 'vlist',
        accumulator: isAccumulator,
        combiner: false,
        combinerMask: '',
        hash: 0,
        filter: "",
        vid: vid,
        checked: false,
        freeze: false,
        frames: {},
        left: 0,
        count: 0,//Количество элементов в списке
        that: that,
        order: {
            /*
             * Сортировка списка.
             * Параметр css order, а значит и установка data:_order для vframe имеет приоритет
             *
             * Если стоит autosorting = true, то сортировка проводится после каждого пинга
             */
            desc: false,
            param: 'id',
            autosorting: true
        },

        setFilter: function (filter, wait) {
            if (wait == undefined) wait = false;
            this.filter = filter;
            this.checked = false;
            this.hash = '';
            if (!wait)vPing();
        },

        sort: function (newOrder, newOrderDesc) {
            $(document).trigger('before_list_sort');
            if (newOrder) {
                this.order.param = newOrder;
                if (newOrderDesc !== undefined)
                    this.order.desc = newOrderDesc;
            }
            var order = this.order.param;
            var desc = this.order.desc;
            var list = this.that;
            var $arr, tempFrame;
            var hasChange = true;
            var stopper = 0; // аварийный предохранитель от зацикливания
            var that, thatData, floatThatData, prev, prevData, floatPrevData;
            var sortKeyPresent = false; // Если будет найден хоть один ключ для сортировки, то переключаем в true

            while (hasChange && stopper++ < 1000) {
                hasChange = false;
                $arr = $(list).children('vframe'); //обновляем структуру фреймов
                if ($arr.length < 2) sortKeyPresent = true;
                for (var i = 1; i < $arr.length; i++) {
                    that = $arr[i];
                    tempFrame = Vortex.frames[$(that).attr('vid')][$(that).attr('mid')];
                    if (!tempFrame) continue;
                    thatData = tempFrame.data;
                    prev = $arr[i - 1];
                    tempFrame = Vortex.frames[$(prev).attr('vid')][$(prev).attr('mid')];
                    if (!tempFrame) continue;
                    prevData = Vortex.frames[$(prev).attr('vid')][$(prev).attr('mid')].data;
                    if (thatData[order] !== undefined) sortKeyPresent = true;
                    /*Обработка занчения null*/
                    if(thatData[order] == null) thatData[order] = "";
                    if(prevData[order] == null) prevData[order] = "";

                    /*
                     * Проверка, сравниваются ли параметры как числа, если да, то производится сравнение именно как числа
                     * иначе, сравниваем как строки.
                     */
                    floatThatData = (typeof thatData[order] == 'string') ? parseFloat(thatData[order].replace(/[\s\-\:]/g, '')) : NaN;
                    floatPrevData = (typeof prevData[order] == 'string') ? parseFloat(prevData[order].replace(/[\s\-\:]/g, '')) : NaN;
                    if (!isNaN(floatThatData) && !isNaN(floatPrevData)) {
                        if ((!desc && floatThatData < floatPrevData)
                            || (desc && floatThatData > floatPrevData)) {
                            list.insertBefore(that, prev);
                            hasChange = true;
                        }
                    } else {
                        if (thatData[order] && prevData[order] && typeof thatData[order].toLowerCase == 'function' && typeof prevData[order].toLowerCase == 'function') {
                            if ((!desc && thatData[order].toLowerCase() < prevData[order].toLowerCase()) || (desc && thatData[order].toLowerCase() > prevData[order].toLowerCase())) {
                                list.insertBefore(that, prev);
                                hasChange = true;
                            }
                        } else {
                            if ((!desc && thatData[order] < prevData[order]) || (desc && thatData[order] > prevData[order])) {
                                list.insertBefore(that, prev);
                                hasChange = true;
                            }
                        }
                    }
                }
            }
            // Если было хоть одно смещение, проводим перезаполнение списка с объединителем
            if (this.combiner && stopper > 1) {
                this._recombine();
            }
            if (!sortKeyPresent) {
                console.log('Попытка сортировки по отсутствующему ключу :' + order);
                console.log(this);
            }
            $(document).trigger('after_list_sort');
        },

        /**
         * Функция производит перезапись html кода в комбинаторе для списков с объединителем
         */
        _recombine: function () {
            if (!this.combiner) return;
            var $list = $(this.that);
            $list.trigger('vlist_before_recombine');
            var combinatedHTML = "";
            $list.find('vframe').each(function () {
                combinatedHTML += $(this).html();
            });
            $list.find('combiner').html(this.combinerMask);
            $list.find('combiner [mark]').html($list.find('combiner [mark]').html() + combinatedHTML);
            $list.trigger('vlist_after_recombine');
        },

        /**
         * Функция вставляет липовый фрейм в список. Этот фрейм несет номер -1 и если список не аккумуляторный,
         * а страничный, то будет удален при первом пинге
         *
         * @param model - класс модели-родителя. Например App.User (. используется как разделитель вместо \ )
         * @param vid - ключ типа vframes. Должен присутствовать в массиве $vframes модели родителя
         * @param pseudoData - эмулятор массива data, для заполнения карточки
         */
        insertTempFrame: function (model, vid, pseudoData) {
            if (Vortex.debug.mode) {
                console.log("Добавление «временного» фрейма в список");
                console.log(this);
                console.log("с параметрами");
                console.log(pseudoData);
            }
            var vidFormated = model.replace('.', '_') + "." + vid;
            if (Vortex.frames[vidFormated]) {
                var keys = Object.keys(Vortex.frames[vidFormated]);
                if (keys.length == 0) {
                    if (Vortex.debug.mode) console.log("Нет образца для копирования");
                    return;
                }
                var card = keys[0];
                Vortex.frames[vidFormated]['-1'] = {};
                Vortex.frames[vidFormated]['-1'].card = Vortex.frames[vidFormated][card].card;
                Vortex.frames[vidFormated]['-1'].that = [];
                Vortex.frames[vidFormated]['-1'].data = pseudoData;
            }
            Vortex.insertNewFrame(model, '-1', vid, this, true);
        },

        /*
         * Функция копирования фрейма из одного списка в другой.
         * Если idFrame не задан, то переноситься будут все фреймы.
         */
        copyFrameToList: function (destination, idFrame) {
            if (Vortex.debug.mode) {
                console.log("Копирование из");
                console.log(this);
                console.log("в список");
                console.log(destination);
                console.log((idFrame) ? " фрейм " + idFrame : "всех фреймов");
            }
            var frameId, frame, model_vid, newFrame;
            var source = this;
            if (Vortex.debug.mode) console.log("Скопировано:");
            if (idFrame === undefined) {
                for (frameId in source.frames) {
                    if (source.frames.hasOwnProperty(frameId)) {
                        frame = source.frames[frameId];
                        if (Vortex.debug.mode) console.log(frame);
                        model_vid = frame.getAttribute('vid').split('.');
                        if (destination.frames[frameId] === undefined) {
                            newFrame = Vortex.insertNewFrame(model_vid[0], frameId, model_vid[1], destination);
                            if (Vortex.debug.mode) console.log(newFrame);
                        }
                    }
                }
            } else {
                frame = source.frames[idFrame];
                model_vid = frame.getAttribute('vid').split('.');
                if (destination.frames[idFrame] === undefined) {
                    Vortex.insertNewFrame(model_vid[0], idFrame, model_vid[1], destination);
                }
            }
            if (Vortex.debug.mode) console.log("Копирование закончено\n");
            destination.sort();
            this._recombine();
        },

        /*
         * Функция очищает текущее наполнение списка, выставляет checked = false и hash = ''
         * HTML код отрисовывает иконку загрузки
         * Если при этом не задана wait=true, то происходит репинг на сервер.
         */
        refill: function (wait) {
            if (wait == undefined) wait = true;
            this.frames = {};
            this.freeze = false;
            this.hash = '';
            this.checked = false;
            if (this.combiner)
                $(this.that).find('combiner').html(Vortex._showLoading());
            else this.that.innerHTML = Vortex._showLoading();
            this.that.removeAttribute('freeze');
            if (!wait) this.setFilter(this.filter);
        }
    }
};
Vortex._premade.newVframe = function (that) {
    return {
        vType: 'vframe',
        id: null,
        vid: '',
        hash: '',
        checked: false,
        card: '',
        that: [that],
        data: {}
    };
};
Vortex._premade.newSwitcher = function (that) {
    return {
        id: null,
        /* Связь с полем vframe элемента*/
        parent: {
            vframe: null, /* vid vframe элемента */
            param: null /* название параметра для мониторинга. Сранение производится после каждого vPing */
        },
        group: null, /* Группа переключателей */
        state: null,
        states: {},
        that: [that],
        set: function (newState, withEvent) {
            if (newState === undefined) {
                newState = this.state;
                withEvent = false;
            }
            if (withEvent === undefined) withEvent = true;
            if (!this.states[newState]) return;
            this.state = newState;
            var tempSwitcher, tempCanvas;
            for (var i = 0; i < this.that.length; i++) {
                tempSwitcher = this.that[i];
                tempCanvas = $(tempSwitcher).find('[canvas]');
                tempSwitcher.setAttribute('state', newState);
                if (tempCanvas.length > 0) tempSwitcher = tempCanvas[0];
                tempSwitcher.innerHTML = this.states[newState].html;

                //Для всех переключателей той же группы нужно выставить нулевое значение
                var that = this;
                var arOffStates = ["", "0", "off"];
                if (arOffStates.indexOf(newState) == -1 && this.group) {
                    $(Vortex.switchesGroups[this.group]).each(function (i, el) {
                        var switcher = Vortex.switches[el];
                        if (switcher !== that) {
                            $(arOffStates).each(function (i, el) {
                                if (switcher.states[el] !== undefined) {
                                    switcher.set(el);
                                }
                            })
                        }
                    })
                }
            }
            if (withEvent) {
                try {
                    eval(this.states[newState].event);
                } catch (e) {
                    if (Vortex.debug.mode) {
                        console.log("Ошибка при переключении switcher " + this.id);
                        console.log(e);
                        console.log();
                    }
                }
            }
        },
        _addState: function (stateName, html, event) {
            this.states[stateName] = {
                html: html.trim().replace(/[\r\n]/g, '').replace(/\>\s+\</g, '> <'),
                event: event.trim().replace(/[\r\n]/g, '').replace(/;\s+/g, ';')
            }
        },
        _checkState: function () {
            if (
                !this.link.vframe
                || !this.link.vframe.data[this.link.param]
                || this.link.vframe.data[this.link.param] == this.state
            ) return;
            this.set(this.link.vframe.data[this.link.param])
        },
        _needControl: false,
        _freeze: false
    }
};
$(document).ready(function () {
    Vortex.ajax._parseElements();
    $('body')
        .append('<div id="vortex_pulse"><img class="svg" src="/img/vortex_pulse.svg"></div>')
        .on('click', '.vortex_scan_btn', function (e) {
            e.stopPropagation();
            Vortex.debug.intoConsole(this);
        })
        .on('click', '[switcher]:not([set]) [set]', function () {
            /**
             * Если на самом переключателе не стоит маркер set,
             * то ищутся дочерние элементы, при клике по которым происходит переключение
             * состояния переключателя на указанное в параметре
             */
            var temp = Vortex.switches[$(this).attr('switcher')];
            if (temp._freeze) return;
            temp._freeze = true;
            setTimeout(function () {
                temp._freeze = false;
            }, Vortex.switcherFreezeDelay);
            var newState = $(this).attr('set');
            if (temp && temp.states[newState]) temp.set(newState);
        })
        .on('click', '[switcher][set]', function () {
            /**
             * Если маркер set стоит на самом переключателе,
             * то при клике по нему происходит цикличное переключение состояний
             */
            try {
                var temp = Vortex.switches[$(this).attr('switcher')];
                if (temp._freeze) return;
                temp._freeze = true;
                setTimeout(function () {
                    temp._freeze = false;
                }, Vortex.switcherFreezeDelay);
                if (temp && temp.states) {
                    var state = temp.state;
                    var states = Object.keys(temp.states);
                    var newState = states[0];
                    for (var i = 0; i < states.length; i++) {
                        if (states[i] == state) {
                            if (i < states.length - 1) newState = states[i + 1];
                        }
                    }
                    temp.set(newState);
                }
            } catch (error) {
                console.log(error);
            }
        });

    $(document).keydown(function (e) {
        if (e.keyCode == 96 && e.ctrlKey && e.altKey) Vortex.debugMode(!Vortex.debug.mode);
    });

    //вытаскиваем svg в код из картинки
    jQuery('img.svg').each(function () {
        var $img = jQuery(this);
        var imgID = $img.attr('id');
        var imgClass = $img.attr('class');
        var imgURL = $img.attr('src');

        jQuery.get(imgURL, function (data) {
            // Get the SVG tag, ignore the rest
            var $svg = jQuery(data).find('svg');

            // Add replaced image ID to the new SVG
            if (typeof imgID !== 'undefined') {
                $svg = $svg.attr('id', imgID);
            }
            // Add replaced image classes to the new SVG
            if (typeof imgClass !== 'undefined') {
                $svg = $svg.attr('class', imgClass + ' replaced-svg');
            }

            // Remove any invalid XML tags as per http://validator.w3.org
            $svg = $svg.removeAttr('xmlns:a');

            // Check if the viewport is set, if the viewport is not set the SVG wont't scale.
            if (!$svg.attr('viewBox') && $svg.attr('height') && $svg.attr('width')) {
                $svg.attr('viewBox', '0 0 ' + $svg.attr('height') + ' ' + $svg.attr('width'))
            }

            // Replace image with new SVG
            $img.replaceWith($svg);

        }, 'xml');

    });
    Vortex.ajax.pingProcess = setInterval(Vortex.ajax._pingFunction, Vortex.pingDelay);
}).on('mousemove', function () {
    Vortex.ajax._timer = Date.now();
    if (Vortex.ajax.sleepmode) Vortex.ajax.sleepMode(false);
}).on('keydown', function () {
    Vortex.ajax._timer = Date.now();
    if (Vortex.ajax.sleepmode) Vortex.ajax.sleepMode(false);
});

/**
 * Функция добавления фрейма для подгрузки с сервера
 *
 * @param model - класс модели-родителя. Например App.User (. используется как разделитель вместо \ )
 * @param id - ID модели в БД
 * @param vid - ключ типа vframes. Должен присутствовать в массиве $vframes модели родителя
 * @param parent - элемент, к котрому прикрепится новый фрейм
 * @param wait - задержка пинга. Если не указана, то считается false
 * @returns {Element}
 */
Vortex.insertNewFrame = function (model, id, vid, parent, wait) {
    if (parent === undefined) parent = document.getElementById('app');
    if (wait === undefined) wait = false;

    var newVFrame = document.createElement('vframe');
    // newVFrame.innerHTML = Vortex._showLoading();
    var vidFormated = model.replace('.', '_') + "." + vid;
    newVFrame.setAttribute("vid", vidFormated);
    newVFrame.setAttribute("mid", id);

    var frame = Vortex._premade.newVframe(newVFrame);
    frame.id = id;
    frame.vid = vid;
    if (this.frames[vidFormated]) {
        var vParent = this.frames[vidFormated][id];
        if (vParent) {
            if (Vortex.debug.mode) console.log('copy vframe ' + vidFormated + ' id:' + id);
            var temp = vParent.that;
            newVFrame.innerHTML = vParent.card;
            $(newVFrame).find("[mark]").each(function () {
                var mark = $(this).attr('mark');
                $(this).html(vParent.data[mark]);
            });
            if (vParent.data) {
                var state = vParent.data['_state'];
                if (state) newVFrame.setAttribute('class', state);
                var order = vParent.data['_order'];
                if (order) newVFrame.setAttribute('style', 'order: ' + order);
            }
            temp.push(newVFrame);
        } else {
            this.frames[vidFormated][id] = frame;
            if (!wait)vPing();
        }
    } else {
        this.frames[vidFormated] = {};
        this.frames[vidFormated][id] = frame;
        if (!wait)vPing();
    }

    //Если дебаг-мод добавляем кнопку "в консоль"
    if (Vortex.debug.mode) {
        $(newVFrame).append(Vortex.debug._btnFrameTemplate);
    }

    /*
     * Если в качестве нового родителя указан html элемент (у него есть нет параметра vType),
     * то просто добавляем фрейм к нему. Если parent - это vlist, то нужно еще зарегистрировать новый фрейм в списке.
     */
    if (parent.vType === 'vlist') {
        if (parent.frames[frame.id] === undefined) {
            parent.that.appendChild(newVFrame);
            parent.frames[frame.id] = newVFrame;
        }
    }
    else
        parent.appendChild(newVFrame);

    //Парсим страницу на предмет свежедобавленных элементов
    Vortex.ajax._parseElements(newVFrame);

    return newVFrame;
};

/**
 * Добавляем новый список
 * @param model Класс
 * @param filter Фильтр
 * @param vid Тип списка (согласно protected static $vlists класса)
 * @param parent Родитель в DOM структуре
 * @param wait Ожидание. Если ожидание стоит false или не указано, то сразу после вставки списка пройдет команда vPing()
 * @returns {Element} DOM Элемент-список
 */
Vortex.insertNewList = function (model, filter, vid, parent, wait, isAccumulator) {
    if (wait == undefined) wait = false;
    if (isAccumulator == undefined) isAccumulator = true;
    if (parent == undefined) parent = document.getElementById('app');
    if (!vid) vid = '';

    var newVList = document.createElement('vlist');
    newVList.innerHTML = Vortex._showLoading();
    var vidFormated = model.replace('.', '_') + "." + vid;
    newVList.setAttribute("vid", vidFormated);

    var list = Vortex._premade.newVlist(newVList, vidFormated, isAccumulator);
    list.filter = filter;
    if (list.filter._order) {
        list.order.desc = false;
        list.order.param = list.filter._order;
    }
    if (list.filter._orderdesc) {
        list.order.desc = true;
        list.order.param = list.filter._orderdesc;
    }
    //Если дебаг-мод добавляем кнопку "в консоль"
    if (Vortex.debug.mode) {
        $(newVList).append(Vortex.debug._btnListTemplate);
    }

    if (!this.lists[vidFormated]) {
        this.lists[vidFormated] = [];
    }
    this.lists[vidFormated].push(list);

    if (!wait) vPing();
    parent.appendChild(newVList);

    //Парсим страницу на предмет свежедобавленных элементов
    Vortex.ajax._parseElements(newVList);

    return newVList;
};

//TODO компрессор json данных?
Vortex.ajax = {
    inProcess: false,
    lastPing: 0,
    stek: [],
    sleepmode: false,
    _timer: 0, //Хранилище вемени последней активности
    _spamBuffer: null, //Последний "подмороженный" процесс
    _spamPingDelay: 300, //Задержка спам-пинга
    _pingFunction: function () {
        if (Date.now() - Vortex.ajax.lastPing > Vortex.pingDelay * 0.7)
            vPing();
        Vortex.ajax.sleepMode(Date.now() - Vortex.ajax._timer > Vortex.sleepDelay);
    },
    sleepMode: function (mode) {
        if (mode == Vortex.ajax.sleepmode) return;
        if (Vortex.debug.mode) console.log((mode) ? "Переход в спящий режим" : "Переход в активный режим");
        Vortex.ajax.sleepmode = mode;
        clearInterval(Vortex.ajax.pingProcess);
        var delay = (!Vortex.ajax.sleepmode) ? Vortex.pingDelay : Vortex.pingLongDelay;
        Vortex.ajax.pingProcess = setInterval(Vortex.ajax._pingFunction, delay);
    }
};

Vortex.send = function (req, callback, route) {
    //Если запрос уже идет, добавить в очередь
    if (this.ajax.inProcess) {
        this.ajax.stek.push({'req': req, 'callback': callback, 'route': route});
        return;
    }
    this.ajax.inProcess = true;
    if (route === undefined) route = "ajax";
    try {
        $.post(
            "/" + route,
            {
                "_token": $("[name=csrf-token]").prop('content'),
                "_session": Vortex.sessionID,
                "data": req
            },
            function (data) {
                //Индикация проблем со связью отключается
                $('#vortex_pulse').removeClass('error');
                if (data.log) {
                    console.log(data.log);
                }
                if (data.debug) {
                    console.log('\nDEBUG:');
                    console.log(data.debug);
                    console.log();
                }
                if (data.msg) {
                    if (typeof data.msg !== "object") data.msg = {text: data.msg};
                    if (!data.msg.text) data.msg.text = "";
                    if (!data.msg.btn) data.msg.btn = "Ok";
                    serviceController.addModal({
                        title: data.msg.title,
                        content: data.msg.text,
                        closeBtn: false,
                        buttons: {
                            Ok: {
                                btnClass: 'btn',
                                text: data.msg.btn
                            }
                        }
                    });
                }

                if (data.error) {
                    serviceController.addModal({
                        title: 'Ошибка',
                        content: data.error,
                        closeBtn: false,
                        buttons: {
                            Ok: {
                                btnClass: 'btn',
                                text: 'Ok'
                            }
                        }
                    });
                } else {
                    try {
                        if (callback != null)
                            callback(data);
                    } catch (error) {
                        console.log("------------------------------\nERROR: ошибка выполнения callback-а с параметрами");
                        console.log("callback:");
                        console.log(callback);
                        console.log("args:");
                        console.log(data);
                        console.log('');
                        console.log(error);
                        console.log('------------------------------');
                    }
                }

                /*
                 *   Запрос выполнения функций от сервера.
                 *   Передается в виде массива объектов {func: название функции ,args: аргументы для функции}
                 */
                if (data && data.events) {
                    for (var i = 0; i < data.events.length; i++) {
                        var e = data.events[i];
                        var func = e.func;
                        var args = e.args;
                        try {
                            window[func](args);
                        } catch (error) {
                            console.log("------------------------------\nERROR: ошибка запроса с сервера на выполнение функций");
                            console.log("function:");
                            console.log(func);
                            console.log("args:");
                            console.log(args);
                            console.log('');
                            console.log(error);
                            console.log('------------------------------');
                        }
                    }
                }

                Vortex.ajax.getFromStek();
            },
            'json'
        ).fail(function (error) {
            //Индикация проблем со связью
            $('#vortex_pulse').addClass('error');
            // if (error.status == 419) {
            //     document.location = document.location;
            // }
            /*
             *  TODO Удалить в релизе
             *  Небольшой костыль для ошибки Laravel при устаревании токена.
             *  Для работы требуется внести изменения в файл VerifyCsrfToken.php в vendor
             */
            if (error.responseText && error.responseText.search('recheck') >= 0) {
                serviceController.addModal({
                    title: 'Внимание',
                    content: "Ваша сессия устарела и была закрыта.\nПожалуйста, подождите. Производится переоткрытие сессии.",
                    closeBtn: false,
                    buttons: {
                        Ok: {
                            btnClass: 'btn',
                            text: 'Ok'
                        }
                    }
                });
                document.location = '/';
            } else {
                serviceController.addModal({
                    title: 'Ошибка',
                    content: "Потеряна связь с сервером.\nДля возобновления нормальной работы требуется перезагрузить страницу.",
                    closeBtn: false,
                    buttons: {
                        Ok: {
                            btnClass: 'btn',
                            text: 'Ok'
                        }
                    }
                });
            }
            clearInterval(Vortex.ajax.pingProcess);

            console.log(error);
        });
    } catch (error) {
        console.log("------------------------------\nERROR: отправки POST запроса.");
        console.log("req:" + req + "; callback" + callback + "; route" + route);
        console.log('');
        console.log(error);
        console.log('------------------------------');

        this.ajax.getFromStek()
    }
};

/*
 * Если есть очередь, то нужно отправить следующий запрос
 */
Vortex.ajax.getFromStek = function () {
    var train = this.stek;
    var next = train.shift();
    var pingInStek = false;
    /*
     * Если из очереди взят ping, то он возвращается в конец очереди. Но только один раз.
     * Повторные пинги будут просто аннулироваться, пока не встретится отличный от ping запрос, или пока
     * не будет извлечен последний запрос, который будет тем самым ping, который мы вернули
     * в конец очереди в начале цикла
     */
    while (next && next.route == 'ping' && train.length > 0) {
        if (!pingInStek) {
            train.push(next);
            pingInStek = true;
        }
        next = train.shift();
    }
    //Отключаем маркер "inProcess"
    Vortex.ajax.inProcess = false;
    if (next) {
        Vortex.send(next.req, next.callback, next.route);
    }
};

/*
 * Фукция для индикации коннекта
 */
Vortex.ajax.pulse = function () {
    if (Vortex.debug.mode)console.log("ping");
    var $v = $('#vortex_pulse');
    $v.addClass('pulse');
    setTimeout(function () {
        $v.removeClass('pulse')
    }, 200);
};

//Функция для обновления данных во фреймах
function vPing() {
    var datenow = Date.now();
    if (Vortex.ajax._spamBuffer || datenow - Vortex.ajax.lastPing < Vortex.ajax._spamPingDelay) {
        /**
         * Если с момента последнего пинга прошло мало времени, создается отложенный процесс, сохраняемый в _spamBuffer.
         * Если за время подморозки появляется новый запрос, то он просто сбрасывается
         */
        if (Vortex.ajax._spamBuffer) return;
        Vortex.ajax._spamBuffer = setTimeout(function () {
            Vortex.ajax._spamBuffer = null;
            vPing();
        }, Vortex.ajax._spamPingDelay + 10);
    }
    Vortex.ajax.pulse();
    Vortex.ajax.lastPing = datenow;
    var keyList = {frames: {}, lists: {}};
    for (var frame in Vortex.frames) {
        if (Vortex.frames.hasOwnProperty(frame)) {
            var temp = Vortex.frames[frame];
            for (var i in temp) {
                if (temp.hasOwnProperty(i) && !temp[i].checked) {
                    temp[i].checked = true;
                    var klID = frame + '.ID' + temp[i].id;
                    keyList.frames[klID] = {
                        vid: frame,
                        id: temp[i].id,
                        hash: temp[i].hash
                    };
                    if (!temp[i].card) {
                        keyList.frames[klID]['card'] = true;
                    }
                }
            }
        }
    }
    for (var list in Vortex.lists) {
        if (Vortex.lists.hasOwnProperty(list)) {
            temp = Vortex.lists[list];
            for (i in temp) {
                if (temp.hasOwnProperty(i) && !temp[i].checked && !temp[i].freeze) {
                    temp[i].checked = true;
                    klID = list + '.ID' + i;
                    keyList.lists[klID] = {
                        id: i,
                        vid: list,
                        filter: JSON.stringify(temp[i].filter),
                        hash: temp[i].hash
                    };
                }
            }
        }
    }
    Vortex.send(JSON.stringify(keyList), function (data) {
        Vortex.ajax.pingCallback(data);
    }, 'v_ping');
}

/*
 * Функция обработки ответа сервера на пинг.
 * Производит сличение и коррекцию данных в вортекс элементах
 *
 * После выполнения функции инициируется событие vPing на document
 * ($(document).trigger('vPing'))
 */
Vortex.ajax.pingCallback = function (data) {
    /**
     * формат данных в data
     * frames - перечень фреймов
     *     ключ Класс.Тип.ID => {card, data, hash, vid, id}
     */
    if (Vortex.debug.mode) {
        console.log('\nОтвет на vPing:');
        console.log(data);
    }
    if (data.responce == undefined) {
        return;
    }
    var listList = data.responce.lists;
    var frameList = data.responce.frames;

    var key, vid, id, card, framedata, hash, frames, parent, childVID, i, lists, switcher;

    /*
     * Для всех полученных фреймов производим сличение хэша
     * Если хэш отличается, производим перезаполнение отметок.
     * Если при этом card == false, то производим подмену html из поля card
     */
    for (key in frameList) {
        if (frameList.hasOwnProperty(key)) {
            var frame = frameList[key];
            try {
                vid = frame.vid;
                id = frame.id;
                card = frame.card;
                framedata = JSON.parse(decodeURIComponent(frame.data));
                hash = frame.hash;

                if (!Vortex.frames[vid]) Vortex.frames[vid] = [];
                if (!Vortex.frames[vid][id])
                    Vortex.frames[vid][id] = {
                        id: id,
                        hash: 0,
                        data: '',
                        checked: true,
                        card: false,
                        that: []
                    };
                parent = Vortex.frames[vid][id];

                //Если новый хэш == wrong, значит модель не найдена в БД. инициируем событие wrong_frame_id
                if (hash == 'wrong') {
                    parent.hash = 'wrong';
                    parent.card = '<div>Нет данных</div>';
                    for (i = 0; i < parent.that.length; i++) {
                        if (vid == 'App_Task.indexcard')
                            $(parent.that[i]).html('<div>задача №' + id + ' не найдена</div>').attr('class', 'wrong');
                        else
                            $(parent.that[i]).html('<div>Нет данных</div>').attr('class', 'wrong');
                    }
                    $(document).trigger('wrong_frame_id');
                    continue;
                }
                if (parent.hash == hash) continue;
                var replaceHTML = !parent.card;
                parent.data = framedata;
                parent.hash = hash;
                for (i = 0; i < parent.that.length; i++) {
                    var that = parent.that[i];
                    if (replaceHTML) that.innerHTML = card;
                    if (parent.data['_state']) $(that).attr('class', parent.data['_state']);
                    if (parent.data['_order']) $(that).css('order', parent.data['_order']);
                    $(that).find("[mark]").each(function () {
                        var mark = $(this).attr('mark');
                        //Проверка, относится ли данная метка к обрабатываемому фрейму или это метка вложенного фрейма
                        if ($(this).closest('vframe')[0] == that)
                            $(this).html(parent.data[mark]);
                    });

                    Vortex.ajax._parseElements(that);
                }
                if (card) parent.card = card;
            } catch (error) {
                console.log("------------------------------\nERROR: Ошибка при заполнении vframe по данным с сервера");
                console.log(frame);
                console.log();
                console.log(error);
                console.log("------------------------------");
            }
        }
    }

    /**
     * Теперь проверяем переключатели на соответствие родительским фреймам.
     * Если находится отклонение, то оно маркируется. В конце, при наличии отклонений
     * проводится дополнительный vPing().
     * Если отклонение сохранилось, проводится правка.
     */
    var switches = Vortex.switches;
    var fNeedPing = false;
    for (key in switches) {
        if (!switches.hasOwnProperty(key)) continue;
        switcher = switches[key];
        parent = switcher.parent;
        if (!parent.vframe) continue;
        if (!parent.vframe.data[parent.param]) continue;
        if (switcher.state != parent.vframe.data[parent.param]) {
            if (switcher._needControl) {
                switcher.set(parent.vframe.data[parent.param]);
                switcher._needControl = false;
            } else {
                switcher._needControl = true;
                fNeedPing = true;
            }
        }
    }

    /*
     * Списки проверяем после фреймов, так как сперва требуется внедрение данных о новых фреймах в архив Vortex
     *
     * Для всех полученных списков производим сличение хэша
     * Если хэш отличается, производим корректировку имеющихся фреймов.
     */
    for (key in listList) {
        if (listList.hasOwnProperty(key)) {
            var model, arr, temp, left, count, today;
            var list = listList[key];
            try {
                vid = list.vid;
                frames = list.frames;
                hash = list.hash;
                id = list.id;
                childVID = list.childVID;
                left = list.left;
                count = list.count;
                today = list.today;

                if (!Vortex.lists[vid]) continue;
                parent = Vortex.lists[vid][id];
                $(parent.that).find('.loading-img').remove();
                if (parent.hash == hash) continue;
                parent.hash = hash;
                parent.left = left;
                parent.count = count;
                parent.filter['_count'] = count;
                // parent.filter['_today'] = today;
                for (i = 0; i < frames.length; i++) {
                    //Проверка, нет ли данного фрейма в списке
                    if (parent.frames[frames[i]]) continue;
                    arr = vid.split('.');
                    model = arr[0];
                    parent.frames[frames[i]] = Vortex.insertNewFrame(model, frames[i], childVID, parent.that);
                }

                //Если список страничного типа (не накопительный) то нужно почистить его от лишних фреймов
                if (!parent.accumulator) {
                    for (i in parent.frames) {
                        if (parent.frames.hasOwnProperty(i)) {
                            if (frames.indexOf(parseInt(i)) < 0) {
                                try {
                                    parent.frames[i].remove();
                                } catch (er) {
                                    console.log(parent.frames[i]);
                                }
                                delete parent.frames[i];
                            }
                        }
                    }
                }
                if (parent.order.autosorting)parent.sort();
                if (parent.combiner)parent._recombine();

                Vortex.ajax._parseElements(parent.that);

            } catch (error) {
                console.log("------------------------------\nERROR: Ошибка при заполнении vlist по данным с сервера");
                console.log(list);
                console.log();
                console.log(error);
                console.log("------------------------------");
            }
        }
    }

    if (data.responce.empty == true) {
        fNeedPing = Vortex.recheck();
    }

    if (fNeedPing) setTimeout(function () {
        vPing()
    }, 100);

    if (Vortex._counter++ > 1) {
        if (Vortex.ajax.stek.length == 0) {
            Vortex._cleanFrames();
        }
        Vortex._counter = 0;
    }
    Vortex._lastResponce = data;
    $(document).trigger('vping_complete');
};

/*
 *   При новом запуске страницы, необходимо почистить память сессии от старой информации.
 */
Vortex.ajax.cleanSession = function () {
    Vortex.ajax._timer = Date.now();
    Vortex.send([], null, 'start_vortex');
};

Vortex.ajax.cleanSession();

Vortex.setFilter = function (listVID, listID, filter) {
    Vortex.lists[listVID][listID].setFilter(filter);
};

/*
 * Удалять указатели на отсутствующие фреймы
 */
Vortex._cleanFrames = function () {
    var num = 0;
    for (var frameVID in Vortex.frames) {
        if (Vortex.frames.hasOwnProperty(frameVID)) {
            var temp = Vortex.frames[frameVID];
            for (var id in temp) {
                if (temp.hasOwnProperty(id)) {
                    for (var i = temp[id].that.length - 1; i >= 0; i--) {
                        //Пустой хэш говорит что объект не получен из БД. Такую карточку нужно удалить
                        if (temp[id].hash == "") {
                            $(temp[id].that).fadeOut();
                            var t = temp[id];
                            setTimeout(function () {
                                $(t.that).remove();
                                console.log("Объект не найден в БД");
                            }, 300);
                            t.that.splice(i, 1);
                            continue;
                        }
                        if ($(temp[id].that[i]).parent().length === 0) {
                            temp[id].that.splice(i, 1);
                            num++;
                        }
                    }
                }
            }
        }
    }
    if (num && Vortex.debug.mode) console.log("Удалено пустых ссылок vframes: " + num);
};

Vortex.action = function (model, method, args) {
    if (args == undefined) args = {};

    Vortex.send({'model': model, 'method': method, 'args': args},
        function () {
            vPing();
        },
        'v_action'
    )
};

Vortex.debugMode = function (debugOn) {
    var $lists = $('vlist');
    var $frames = $('vframe');
    var $switchers = $('[switcher]');
    if (debugOn !== true) {
        this.debug.mode = false;
        $lists.find(' .vortex_scan_btn').remove();
        $frames.find(' .vortex_scan_btn').remove();
        $switchers.find(' .vortex_scan_btn').remove();
        console.log('Vortex Debug Mode остановлен');
        return;
    }
    console.log('Vortex Debug Mode запущен');
    this.debug.mode = true;
    $lists.append(Vortex.debug._btnListTemplate);
    $frames.append(Vortex.debug._btnFrameTemplate);
    $switchers.append(Vortex.debug._btnSwitcherTemplate);
};

var vTemp;
Vortex.debug.intoConsole = function (that) {
    var element = $(that).parent();
    var arr, i, target;
    var switcher = element[0].getAttribute('switcher');
    if (switcher) {
        target = Vortex.switches[switcher];
    } else {
        if (element[0].nodeName == 'VLIST') {
            arr = Vortex.lists[element.attr('vid')];
            for (i in arr) {
                if (arr.hasOwnProperty(i)) {
                    if (arr[i].that === element[0]) {
                        target = arr[i];
                        break;
                    }
                }
            }
        } else {
            target = Vortex.frames[element.attr('vid')][element.attr('mid')];
        }
    }
    if (target) {
        vTemp = target;
        console.log(target);
        if (i !== undefined)console.log("list ID:" + i);
        if (switcher) console.log("switcher ID:«" + switcher + "»");
        if (element.attr('mid')) console.log("vframe vID:«" + element.attr('vid') + "»");
        console.log('переменная сохранена как vTemp');
    }
};

Vortex.ajax._parseElements = function (that) {
    var stopper = 0;
    var $container = (that) ? $(that) : $('body');
    /*
     * Цикл для переотрисовки всех вложеных списков и фреймов
     */
    while ($container.find('hash').length > 0 && stopper++ < 10000) {

        var combinerList = [];
        //Собираем список всех vlist
        $container.find('vlist').each(function () {
            if ($(this).children('hash').length == 0) return;
            var vid = $(this).attr('vid');
            var list = Vortex._premade.newVlist($(this)[0], vid);
            list.hash = $(this).children('hash').text();
            list.filter = JSON.parse($(this).children('filter').text());
            if ($(this).attr('combiner') !== undefined) {
                list.combiner = true;
                list.combinerMask = $(this).find('combiner').html();
                combinerList.push(list);
            }
            if (list.filter['_left']) {
                list.left = list.filter['_left'];
            }
            if (list.filter['_count']) {
                list.count = list.filter['_count'];
            }
            if (list.filter['_order']) {
                list.order.desc = false;
                list.order.param = list.filter['_order'];
            }
            if (list.filter['_orderdesc']) {
                list.order.desc = true;
                list.order.param = list.filter['_orderdesc'];
            }
            if ($(this).attr('page') !== undefined) {
                list.accumulator = false;
                $(this).removeAttr('page');
            }
            /*
             * Наполняем перечень фреймов.
             * Ищем по <id>, так как у обработанных элементов этот блок отсутствует
             */
            list.frames = {};
            $(this).find('id').each(function () {
                list.frames[$(this).text()] = $(this).parent()[0];
            });

            $(this).children("filter").remove();
            $(this).children("hash").remove();
            if ($(this).attr('freeze') !== undefined) {
                $(this).html(Vortex._showLoading());
                list.freeze = true;
            }

            if (!Vortex.lists[vid])Vortex.lists[vid] = [];
            Vortex.lists[vid].push(list);
        });

        //Собираем список всех vframe
        $container.find('vframe').each(function () {
            if ($(this).children('hash').length == 0) return;
            var frame = Vortex._premade.newVframe($(this)[0]);
            var vid = $(this).attr('vid');
            frame.vid = vid.split('.')[1];
            frame.id = $(this).children('id').text();
            frame.hash = $(this).children('hash').text();

            //Проверяем, нет ли такого же вифрейма в памяти
            if (Vortex.frames[vid] && Vortex.frames[vid][frame.id]) {
                //Если есть, берем данные из него
                frame.data = Vortex.frames[vid][frame.id].data;
                Vortex.frames[vid][frame.id].that.push($(this)[0]);
            } else {
                frame.data = JSON.parse(decodeURIComponent($(this).children('data').text()));
                if (!Vortex.frames[vid])Vortex.frames[vid] = {};
                Vortex.frames[vid][frame.id] = frame;
            }
            $(this).children("id").remove();
            $(this).children("data").remove();
            $(this).children("hash").remove();
            frame.card = $(this).html();

            if (frame.data) {
                $(this).find("[mark]").each(function () {
                    var mark = $(this).attr('mark');
                    $(this).html(frame.data[mark]);
                });

                if (frame.data['_state']) $(this).attr('class', frame.data['_state']);
                if (frame.data['_order']) $(this).css('order', frame.data['_order']);
            }
        });

        //Проводим заполнение всех списков с объединителем
        $(combinerList).each(function (i, el) {
            el._recombine();
        })
    }
    while ($container.find('state').length > 0 && stopper++ < 10000) {
        //Собираем список всех switcher элементов
        $container.find('[switcher]').each(function () {
            if ($(this).children('state').length == 0) return;
            var switcher = Vortex._premade.newSwitcher($(this)[0]);
            switcher.id = $(this).attr('switcher');
            switcher.state = ($(this).attr('state')) ? $(this).attr('state') : '';
            if ($(this).attr('group')) {
                switcher.group = $(this).attr('group');
                if (!Vortex.switchesGroups[$(this).attr('group')]) Vortex.switchesGroups[$(this).attr('group')] = [];
                Vortex.switchesGroups[$(this).attr('group')].push(switcher.id);
                $(this).removeAttr('group');
            }
            //Проверяем, нет ли такого же вифрейма в памяти
            if (Vortex.switches[switcher.id]) {
                //Если есть, добавляем в список указателей оставляя старое состояние для указателей
                Vortex.switches[switcher.id].that.push($(this)[0]);
                Vortex.switches[switcher.id].set();
            } else {
                var parent = $(this).children('parent').text();
                var temp = parent.split(' ');
                if (temp.length > 2 && temp[0] && temp[1] && Vortex.frames[temp[0]] && Vortex.frames[temp[0]][temp[1]]) {
                    switcher.parent.vframe = Vortex.frames[temp[0]][temp[1]];
                    switcher.parent.param = temp[2];
                }
                //Регистрируем состояния
                var stateName;
                $(this).children('state').each(function () {
                    stateName = $(this).attr('set');
                    if (!stateName) stateName = '';
                    var event = $(this).children('event').text();
                    $(this).children('event').remove();
                    switcher._addState(stateName, $(this).html(), event)
                });
                Vortex.switches[switcher.id] = switcher;
                Vortex.switches[switcher.id].set(switcher.state, false);
            }
            $(this).children("parent").remove();
            $(this).children("state").remove();
        });
    }
};

/*
 * Поиск элемента vlist в архиве Vortex, соответствуюшего DOM элементу.
 * Если передан массив jQuery (имеется параметр length >0) то работаем с нулевым элементом
 */
Vortex.searchList = function (element) {
    var i, arr, target;
    if (element.length > 0) element = element[0];
    if (element.nodeName == 'VLIST') {
        arr = Vortex.lists[element.getAttribute('vid')];
        for (i in arr) {
            if (arr.hasOwnProperty(i)) {
                if (arr[i].that === element) {
                    target = arr[i];
                    break;
                }
            }
        }
    }
    return target;
};

/*
 * Поиск элемента vframe в архиве Vortex, содержащего указатель на DOM элемент
 */
Vortex.searchFrame = function (element) {
    var target;
    if (element.length > 0) element = element[0];
    if (element.nodeName == 'VFRAME') {
        target = Vortex.frames[element.getAttribute('vid')][element.getAttribute('mid')];
    }
    return target;
};

window.onblur = function () {
    Vortex._lostFocusTime = Date.now();
};

window.onfocus = function () {
    var unfocusedTimeMax = 300000;
    if (Vortex._lostFocusTime == 0) return;
    var lostTime = Date.now() - Vortex._lostFocusTime;
    Vortex._lostFocusTime = 0;
    if (lostTime < unfocusedTimeMax) return;
    console.log("Неактивность " + parseInt(lostTime / 60000) + "мин");
    if (Vortex.recheck()) vPing();
};

