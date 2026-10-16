// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Show/hide toggle for every password field: wraps the input and adds an eye button.
(function () {
    var eye = '<svg class="ico-sm" width="18" height="18"><use href="#i-eye"/></svg>';
    var eyeOff = '<svg class="ico-sm" width="18" height="18"><use href="#i-eye-off"/></svg>';

    document.querySelectorAll('input[type="password"]').forEach(function (input) {
        var wrap = document.createElement('div');
        wrap.className = 'password-field';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'password-toggle';
        btn.setAttribute('aria-label', 'Show password');
        btn.setAttribute('aria-pressed', 'false');
        btn.innerHTML = eye;
        wrap.appendChild(btn);

        btn.addEventListener('click', function () {
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.innerHTML = show ? eyeOff : eye;
            btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            btn.setAttribute('aria-pressed', String(show));
            input.focus();
        });
    });
})();

// Searchable country-code picker: replaces a <select class="phone-country"> (kept hidden for form posting).
(function () {
    var chevron = '<svg class="cc-chevron" aria-hidden="true"><use href="#i-chevron-down"/></svg>';

    document.querySelectorAll('select.phone-country').forEach(function (select, n) {
        var picker = document.createElement('div');
        picker.className = 'cc-picker';
        var listId = 'cc-list-' + n;
        picker.innerHTML =
            '<button type="button" class="cc-trigger" aria-haspopup="listbox" aria-expanded="false" aria-controls="' + listId + '"></button>' +
            '<div class="cc-menu" hidden>' +
            '<input type="text" class="cc-search" placeholder="Search country or code" aria-label="Search country" autocomplete="off" />' +
            '<ul class="cc-list" role="listbox" id="' + listId + '"></ul></div>';
        select.parentNode.insertBefore(picker, select);
        select.hidden = true;
        picker.appendChild(select);

        var trigger = picker.querySelector('.cc-trigger');
        var menu = picker.querySelector('.cc-menu');
        var search = picker.querySelector('.cc-search');
        var list = picker.querySelector('.cc-list');
        var options = Array.prototype.map.call(select.options, function (o) {
            return { value: o.value, flag: o.dataset.flag, dial: o.dataset.dial, name: o.dataset.name };
        });
        var visible = [], active = 0;

        function renderTrigger() {
            var o = options[select.selectedIndex] || options[0];
            trigger.innerHTML = '<span class="cc-flag">' + o.flag + '</span><span>' + o.dial + '</span>' + chevron;
            trigger.setAttribute('aria-label', 'Country code: ' + o.name + ' ' + o.dial);
        }

        function renderList() {
            var q = search.value.trim().toLowerCase().replace(/^\+/, '');
            // Rank: ISO code / name prefix / word prefix / dial-code prefix, then any substring of the name.
            function rank(o) {
                var name = o.name.toLowerCase();
                if (!q) return 0;
                if (o.value.toLowerCase() === q || name.indexOf(q) === 0) return 1;
                if (name.split(/\s+/).some(function (w) { return w.indexOf(q) === 0; })) return 2;
                if (o.dial.replace('+', '').indexOf(q) === 0) return 3;
                if (name.indexOf(q) !== -1) return 4;
                return -1;
            }
            visible = options
                .map(function (o, i) { return { o: o, r: rank(o), i: i }; })
                .filter(function (x) { return x.r !== -1; })
                .sort(function (a, b) { return a.r - b.r || a.i - b.i; })
                .map(function (x) { return x.o; });
            active = q ? 0 : Math.max(0, visible.findIndex(function (o) { return o.value === select.value; }));
            list.innerHTML = visible.length ? '' : '<li class="cc-empty">No matching country</li>';
            visible.forEach(function (o, i) {
                var li = document.createElement('li');
                li.className = 'cc-option' + (i === active ? ' active' : '');
                li.setAttribute('role', 'option');
                li.setAttribute('aria-selected', String(o.value === select.value));
                li.innerHTML = '<span class="cc-flag">' + o.flag + '</span><span class="cc-name">' + o.name + '</span><span class="cc-dial">' + o.dial + '</span>';
                li.addEventListener('mousedown', function (e) { e.preventDefault(); choose(o); });
                li.addEventListener('mousemove', function () { setActive(i); });
                list.appendChild(li);
            });
        }

        function setActive(i) {
            var items = list.querySelectorAll('.cc-option');
            if (!items.length) return;
            active = (i + items.length) % items.length;
            items.forEach(function (el, k) { el.classList.toggle('active', k === active); });
            items[active].scrollIntoView({ block: 'nearest' });
        }

        function open() {
            menu.hidden = false;
            picker.classList.add('open');
            trigger.setAttribute('aria-expanded', 'true');
            search.value = '';
            renderList();
            search.focus();
            setActive(active);
        }

        function close(refocus) {
            menu.hidden = true;
            picker.classList.remove('open');
            trigger.setAttribute('aria-expanded', 'false');
            if (refocus) trigger.focus();
        }

        function choose(o) {
            select.value = o.value;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            renderTrigger();
            close(false);
            var phone = picker.parentNode.querySelector('input[type="tel"], input[inputmode="tel"]');
            (phone || trigger).focus();
        }

        trigger.addEventListener('click', function () { menu.hidden ? open() : close(true); });
        trigger.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); open(); }
        });
        search.addEventListener('input', renderList);
        search.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); setActive(active + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(active - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); if (visible[active]) choose(visible[active]); }
            else if (e.key === 'Escape') { e.preventDefault(); close(true); }
            else if (e.key === 'Tab') { close(false); }
        });
        document.addEventListener('mousedown', function (e) {
            if (!picker.contains(e.target)) close(false);
        });

        renderTrigger();
    });
})();

// Password strength meter: <input data-strength-meter="id-of-.pw-strength">.
(function () {
    var labels = ['', 'Weak', 'Fair', 'Good', 'Strong'];

    document.querySelectorAll('input[data-strength-meter]').forEach(function (input) {
        var meter = document.getElementById(input.dataset.strengthMeter);
        if (!meter) return;
        var label = meter.querySelector('.pw-label');
        var hint = label.textContent;

        input.addEventListener('input', function () {
            var v = input.value, score = 0;
            if (v.length >= 6) score++;
            if (v.length >= 10) score++;
            if (/[a-z]/.test(v) && /[A-Z]/.test(v)) score++;
            if (/\d/.test(v)) score++;
            if (/[^A-Za-z0-9]/.test(v)) score++;
            var level = !v ? 0 : v.length < 6 ? 1 : Math.min(4, Math.max(1, score - 1));
            meter.dataset.level = level;
            label.textContent = level ? labels[level] + ' password' : hint;
        });
    });
})();

// Collapsible desktop sidebar; the choice is remembered per browser.
(function () {
    var root = document.documentElement;
    document.querySelectorAll('[data-sidebar-toggle]').forEach(function (btn) {
        function sync() {
            var collapsed = root.classList.contains('sidebar-collapsed');
            var label = collapsed ? 'Expand sidebar' : 'Collapse sidebar';
            btn.setAttribute('aria-label', label);
            btn.setAttribute('title', label);
            btn.setAttribute('aria-expanded', String(!collapsed));
        }
        btn.addEventListener('click', function () {
            root.classList.toggle('sidebar-collapsed');
            try { localStorage.setItem('pfms.sidebar', root.classList.contains('sidebar-collapsed') ? 'collapsed' : 'expanded'); } catch (e) { }
            sync();
        });
        sync();
    });
})();

// Command menu: ⌘K / Ctrl+K (or any [data-command-open]) to search pages and quick actions.
(function () {
    var el = document.getElementById('commandMenu');
    if (!el || !window.bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(el);
    var input = el.querySelector('.command-input');
    var items = Array.prototype.slice.call(el.querySelectorAll('.command-item'));
    var groups = Array.prototype.slice.call(el.querySelectorAll('.command-group'));
    var empty = el.querySelector('.command-empty');
    var visible = items, active = 0;

    function setActive(i) {
        if (!visible.length) return;
        active = (i + visible.length) % visible.length;
        items.forEach(function (it) { it.classList.remove('active'); it.setAttribute('aria-selected', 'false'); });
        visible[active].classList.add('active');
        visible[active].setAttribute('aria-selected', 'true');
        visible[active].scrollIntoView({ block: 'nearest' });
    }

    function filter() {
        var q = input.value.trim().toLowerCase();
        visible = items.filter(function (it) {
            var hay = (it.textContent + ' ' + (it.dataset.keywords || '')).toLowerCase();
            var show = !q || q.split(/\s+/).every(function (w) { return hay.indexOf(w) !== -1; });
            it.hidden = !show;
            return show;
        });
        // Hide a group heading when none of its items are visible.
        groups.forEach(function (g) {
            var n = g.nextElementSibling, any = false;
            while (n && !n.classList.contains('command-group') && !n.classList.contains('command-empty')) {
                if (!n.hidden) any = true;
                n = n.nextElementSibling;
            }
            g.hidden = !any;
        });
        empty.hidden = visible.length > 0;
        setActive(0);
    }

    items.forEach(function (it) {
        it.setAttribute('role', 'option');
        it.addEventListener('mousemove', function () { var i = visible.indexOf(it); if (i !== -1 && i !== active) setActive(i); });
    });
    input.addEventListener('input', filter);
    input.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowDown') { e.preventDefault(); setActive(active + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); setActive(active - 1); }
        else if (e.key === 'Enter' && visible[active]) { e.preventDefault(); window.location.href = visible[active].href; }
    });
    el.addEventListener('shown.bs.modal', function () { input.focus(); });
    el.addEventListener('show.bs.modal', function () { input.value = ''; filter(); });

    document.querySelectorAll('[data-command-open]').forEach(function (b) {
        b.addEventListener('click', function () { modal.show(); });
    });
    document.addEventListener('keydown', function (e) {
        if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
            e.preventDefault();
            el.classList.contains('show') ? modal.hide() : modal.show();
        }
    });
})();

// Toast notifications: stack bottom-left, auto-dismiss (paused while hovered/focused), closable.
// Server messages are rendered into #toastStack; call window.showToast(message, 'success' | 'error' | 'info') from scripts.
(function () {
    var stack = document.getElementById('toastStack');
    if (!stack) return;

    function dismiss(t) {
        if (t.dataset.leaving) return;
        t.dataset.leaving = '1';
        t.classList.add('is-leaving');
        setTimeout(function () { t.remove(); }, 230);
    }

    function arm(t) {
        var duration = parseInt(t.dataset.duration, 10) || 5000;
        var remaining = duration, startedAt = Date.now(), timer;
        t.style.setProperty('--toast-duration', duration + 'ms');

        function start() { startedAt = Date.now(); timer = setTimeout(function () { dismiss(t); }, remaining); }
        function pause() { clearTimeout(timer); remaining -= Date.now() - startedAt; }

        t.addEventListener('mouseenter', pause);
        t.addEventListener('mouseleave', start);
        t.addEventListener('focusin', pause);
        t.addEventListener('focusout', start);
        t.querySelector('.toast-close').addEventListener('click', function () { dismiss(t); });
        requestAnimationFrame(function () { requestAnimationFrame(function () { t.classList.add('is-visible'); }); });
        start();
    }

    window.showToast = function (message, type, duration) {
        type = type || 'success';
        var titles = { success: 'Success', error: 'Something went wrong', info: 'Heads up' };
        var icons = { success: '#i-check', error: '#i-alert', info: '#i-bell' };
        var t = document.createElement('div');
        t.className = 'toast-fin is-' + type;
        t.setAttribute('role', type === 'error' ? 'alert' : 'status');
        t.dataset.duration = duration || (type === 'error' ? 8000 : 5000);
        t.innerHTML =
            '<span class="toast-ico"><svg class="ico-sm"><use href="' + icons[type] + '"/></svg></span>' +
            '<span class="toast-body"><span class="toast-title"></span><span class="toast-msg"></span></span>' +
            '<button type="button" class="toast-close" aria-label="Dismiss notification"><svg class="ico-sm"><use href="#i-x"/></svg></button>' +
            '<span class="toast-timer" aria-hidden="true"></span>';
        t.querySelector('.toast-title').textContent = titles[type] || titles.success;
        t.querySelector('.toast-msg').textContent = message;
        stack.appendChild(t);
        arm(t);
        // Keep the stack short: drop the oldest beyond 4.
        var all = stack.querySelectorAll('.toast-fin:not(.is-leaving)');
        if (all.length > 4) dismiss(all[0]);
        return t;
    };

    stack.querySelectorAll('.toast-fin').forEach(arm);
})();

// Tabs ([data-tabs] tablist of <a href="#panel-id" role="tab">): shows one panel at a time.
// The URL hash picks the tab; a panel with validation errors wins so errors are never hidden.
(function () {
    document.querySelectorAll('[data-tabs]').forEach(function (list) {
        var tabs = Array.prototype.slice.call(list.querySelectorAll('[role="tab"]'));
        var panels = tabs.map(function (t) { return document.getElementById(t.getAttribute('aria-controls')); });

        function show(index, opts) {
            opts = opts || {};
            tabs.forEach(function (t, i) {
                var on = i === index;
                t.classList.toggle('active', on);
                t.setAttribute('aria-selected', String(on));
                t.tabIndex = on ? 0 : -1;
                if (panels[i]) panels[i].hidden = !on;
            });
            if (opts.focus) tabs[index].focus();
            if (opts.updateUrl) history.replaceState(null, '', '#' + panels[index].id);
        }

        function indexForHash() {
            var id = location.hash.slice(1);
            return panels.findIndex(function (p) { return p && p.id === id; });
        }

        tabs.forEach(function (t, i) {
            t.addEventListener('click', function (e) { e.preventDefault(); show(i, { updateUrl: true }); });
            t.addEventListener('keydown', function (e) {
                var next = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[e.key];
                if (next) { e.preventDefault(); show((i + next + tabs.length) % tabs.length, { focus: true, updateUrl: true }); }
                else if (e.key === 'Home') { e.preventDefault(); show(0, { focus: true, updateUrl: true }); }
                else if (e.key === 'End') { e.preventDefault(); show(tabs.length - 1, { focus: true, updateUrl: true }); }
            });
        });
        window.addEventListener('hashchange', function () { var i = indexForHash(); if (i !== -1) show(i); });

        var withErrors = panels.findIndex(function (p) {
            return p && Array.prototype.some.call(p.querySelectorAll('.field-error, .validation-summary-errors'),
                function (el) { return el.textContent.trim().length > 0; });
        });
        // Currency lives in "preferences" but posts with the profile form, so profile errors may come from there too.
        var start = withErrors !== -1 ? withErrors : Math.max(0, indexForHash());
        show(start, { updateUrl: withErrors !== -1 });
        window.scrollTo(0, 0); // don't jump to the anchor; the tab is already in view
    });
})();

// Chart tooltips: any element with data-tooltip='{"title":"…","rows":[{"label","value","color"}]}'
// (built server-side by Helpers/ChartTip.cs). Follows the pointer; also works with keyboard focus and tap.
(function () {
    var tip = document.createElement('div');
    tip.className = 'chart-tip';
    tip.setAttribute('role', 'tooltip');
    tip.hidden = true;
    document.body.appendChild(tip);
    var current = null;

    function render(el) {
        var data;
        try { data = JSON.parse(el.getAttribute('data-tooltip')); } catch (e) { return false; }
        tip.textContent = '';
        var title = document.createElement('div');
        title.className = 'chart-tip-title';
        title.textContent = data.title || '';
        tip.appendChild(title);
        (data.rows || []).forEach(function (r) {
            var row = document.createElement('div');
            row.className = 'chart-tip-row';
            var key = document.createElement('span');
            key.className = 'chart-tip-key';
            if (r.color) { var dot = document.createElement('i'); dot.style.background = r.color; key.appendChild(dot); }
            key.appendChild(document.createTextNode(r.label));
            var val = document.createElement('strong');
            val.textContent = r.value;
            row.appendChild(key); row.appendChild(val);
            tip.appendChild(row);
        });
        return true;
    }

    function place(x, y) {
        var pad = 12, w = tip.offsetWidth, h = tip.offsetHeight;
        var left = x + 14, top = y - h - 14;
        if (left + w + pad > window.innerWidth) left = x - w - 14;   // flip left near the right edge
        if (top < pad) top = y + 18;                                  // flip below near the top
        tip.style.left = Math.max(pad, left) + 'px';
        tip.style.top = Math.max(pad, top) + 'px';
    }

    function show(el, x, y) {
        if (!render(el)) return;
        if (current && current !== el) current.classList.remove('is-tipped');
        current = el;
        el.classList.add('is-tipped');
        tip.hidden = false;
        if (x == null) { var r = el.getBoundingClientRect(); x = r.left + r.width / 2; y = r.top; }
        place(x, y);
    }

    function hide() {
        tip.hidden = true;
        if (current) current.classList.remove('is-tipped');
        current = null;
    }

    document.addEventListener('pointerover', function (e) {
        var el = e.target.closest && e.target.closest('[data-tooltip]');
        if (el) show(el, e.clientX, e.clientY);
    });
    document.addEventListener('pointermove', function (e) {
        if (!current) return;
        var el = e.target.closest && e.target.closest('[data-tooltip]');
        if (el === current) place(e.clientX, e.clientY);
        else if (el) show(el, e.clientX, e.clientY);
        else if (e.pointerType === 'mouse') hide();
    });
    document.addEventListener('pointerout', function (e) {
        if (current && e.pointerType === 'mouse' && !(e.relatedTarget && e.relatedTarget.closest && e.relatedTarget.closest('[data-tooltip]') === current)) hide();
    });
    document.addEventListener('focusin', function (e) {
        var el = e.target.closest && e.target.closest('[data-tooltip]');
        if (el) show(el); else hide();
    });
    document.addEventListener('focusout', function () { hide(); });
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') hide(); });
    window.addEventListener('scroll', hide, { passive: true });
})();

// "Sort by" dropdowns that combine column + direction in one option value ("progress:desc").
(function () {
    document.querySelectorAll('[data-sort-select]').forEach(function (select) {
        select.addEventListener('change', function () {
            var form = select.closest('[data-sort-form]');
            var parts = select.value.split(':');
            form.querySelector('input[name="sort"]').value = parts[0];
            form.querySelector('input[name="dir"]').value = parts[1] || 'asc';
            form.submit();
        });
    });
})();

// Pill/tab groups (the Income period filter, the reports nav): a sliding green thumb
// follows the selected pill. Each pill is still a normal link to a new page, so on
// click we animate the thumb across to it, then follow the link once the slide lands —
// giving a smooth "move from one to another" instead of an instant colour swap.
(function () {
    var SLIDE_MS = 250;

    function init(group) {
        var pills = Array.prototype.slice.call(group.children).filter(function (el) {
            return el.classList.contains('report-pill');
        });
        var active = pills.filter(function (p) { return p.classList.contains('active'); })[0];
        if (pills.length < 2 || !active) return; // nothing to slide between

        var thumb = document.createElement('span');
        thumb.className = 'pill-thumb';
        thumb.setAttribute('aria-hidden', 'true');
        group.insertBefore(thumb, group.firstChild);
        group.classList.add('has-thumb');

        function place(pill, animate) {
            var g = group.getBoundingClientRect();
            var r = pill.getBoundingClientRect();
            var x = r.left - g.left + group.scrollLeft;
            var y = r.top - g.top;
            if (!animate) thumb.style.transition = 'none';
            thumb.style.width = r.width + 'px';
            thumb.style.height = r.height + 'px';
            thumb.style.transform = 'translate(' + x + 'px,' + y + 'px)';
            if (!animate) {
                void thumb.offsetWidth; // flush the instant move before re-enabling the transition
                thumb.style.transition = '';
            }
        }

        place(active, false);

        pills.forEach(function (pill) {
            pill.addEventListener('click', function (e) {
                // Let the browser handle "open in new tab" clicks and re-clicking the current pill as usual.
                if (pill.classList.contains('active') || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
                e.preventDefault();
                pills.forEach(function (p) { p.classList.toggle('active', p === pill); });
                place(pill, true);
                window.setTimeout(function () { window.location.href = pill.href; }, SLIDE_MS);
            });
        });

        window.addEventListener('resize', function () {
            var current = pills.filter(function (p) { return p.classList.contains('active'); })[0];
            if (current) place(current, false);
        });
    }

    document.querySelectorAll('.report-pill-group, .reports-nav').forEach(init);
})();
