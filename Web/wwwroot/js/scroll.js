window.sammlarrScroll = {
    toTop: function () {
        const main = document.querySelector('main');
        const candidates = main ? [document.scrollingElement, main, ...main.querySelectorAll('*')] : [document.scrollingElement];
        for (const element of candidates) {
            if (element && element.scrollTop > 0) {
                element.scrollTo({ top: 0, behavior: 'smooth' });
            }
        }
    },

    intoView: function (id) {
        const element = document.getElementById(id);
        if (element) {
            element.scrollIntoView({ block: "nearest", behavior: "smooth" });
        }
    },
};
