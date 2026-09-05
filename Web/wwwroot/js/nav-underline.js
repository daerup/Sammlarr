(() => {
	const LIST_SELECTOR = "header nav > ul";
	const ACTIVE_SELECTOR = '.nav-link[aria-current="page"]';

	let list = null;
	let listObserver = null;
	let frame = 0;

	const schedule = () => {
		if (frame) return;
		frame = requestAnimationFrame(() => {
			frame = 0;
			measure();
		});
	};

	const measure = () => {
		if (!list || !list.isConnected) {
			attach();
			return;
		}
		const active = list.querySelector(ACTIVE_SELECTOR);
		if (!active) {
			list.style.setProperty("--underline-width", "0px");
			list.dataset.underlineReady = "true";
			return;
		}
		const listRect = list.getBoundingClientRect();
		const linkRect = active.getBoundingClientRect();
		list.style.setProperty("--underline-left", `${linkRect.left - listRect.left}px`);
		list.style.setProperty("--underline-width", `${linkRect.width}px`);
		list.dataset.underlineReady = "true";
	};

	const attach = () => {
		const candidate = document.querySelector(LIST_SELECTOR);
		if (!candidate || candidate === list) return;
		listObserver?.disconnect();
		list = candidate;
		listObserver = new MutationObserver(schedule);
		listObserver.observe(list, { subtree: true, attributes: true, attributeFilter: ["aria-current"] });
		schedule();
	};

	const bootObserver = new MutationObserver(() => {
		if (!list || !list.isConnected) attach();
	});

	const start = () => {
		attach();
		bootObserver.observe(document.body, { childList: true, subtree: true });
		window.addEventListener("resize", schedule);
	};

	if (document.readyState === "loading") {
		document.addEventListener("DOMContentLoaded", start);
	} else {
		start();
	}
})();
