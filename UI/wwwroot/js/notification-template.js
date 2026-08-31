window.sammlarrTemplate = {
    resolve: function (defaultId) {
        let el = document.activeElement;
        if (!el || (el.tagName !== 'TEXTAREA' && el.tagName !== 'INPUT')) {
            el = document.getElementById(defaultId);
        }
        return el;
    },
    write: function (el, text) {
        const start = el.selectionStart ?? el.value.length;
        const end = el.selectionEnd ?? el.value.length;
        el.value = el.value.substring(0, start) + text + el.value.substring(end);
        const caret = start + text.length;
        el.selectionStart = el.selectionEnd = caret;
        el.focus();
        el.dispatchEvent(new Event('input', { bubbles: true }));
    },
    insertToken: function (defaultId, text) {
        const el = this.resolve(defaultId);
        if (el) {
            this.write(el, text);
        }
    },
    insertItem: function (defaultId, itemToken, listExpression) {
        const el = this.resolve(defaultId);
        if (!el) {
            return;
        }
        const before = el.value.substring(0, el.selectionStart ?? el.value.length);
        const inLoop = before.lastIndexOf('{% for') > before.lastIndexOf('{% endfor');
        this.write(el, inLoop ? itemToken : '{% for item in ' + listExpression + ' %}' + itemToken + '\n{% endfor %}');
    }
};
