const MIN_W = 1120;
const MIN_H = 440;
const LIST_X = 327;
const DB_X = 455;
const NODE_R = 22;
const NODE_GAP = 86;
const COL_GAP = 130;
const DETAIL_ROWS = 4;
const TOP = 64;
const BOTTOM = 84;
const SLOT_TOP = 38;
const SLOT_H = 28;
const DELAY = 250;
const LATE = 1000;
const LINGER = 650;
const FRESH = 2200;
const QUEUE = 5;
const MAX_DOTS = 180;
const RING = 2 * Math.PI * NODE_R;
const WAIT_RING = 2 * Math.PI * (NODE_R + 5);
const SVG_NS = 'http://www.w3.org/2000/svg';
const REDUCED = matchMedia('(prefers-reduced-motion: reduce)');

const KIND = ['started', 'sourceEnded', 'listed', 'known', 'earlier', 'path', 'asked', 'answered', 'gaveUp', 'settled', 'page'];
const OUTCOME = ['answered', 'after429', 'gaveup', 'failed'];
const FATE_KEY = ['matched', 'couldnot', 'notfound', 'conflict'];
const FATE_NODE = { matched: 'sink', couldnot: 'cl', notfound: 'nf', conflict: 'cl' };
const OUTCOMES = {
    sink: { fate: 'matched', meaning: 'matched', also: { count: 'known', meaning: 'known', label: 'already known' } },
    cl: { fate: 'couldnot', meaning: 'couldNotLookUp', also: { count: 'conflict', meaning: 'conflict', label: 'in conflict' } },
    nf: { fate: 'notfound', meaning: 'notFound', also: { count: 'cached', meaning: 'notFoundEarlier', label: 'not found earlier' } },
};
const FATE = {
    matched: { label: 'Matched', css: 'var(--f-matched)', glyph: 'check' },
    notfound: { label: 'Not found', css: 'var(--f-notfound)', glyph: 'x' },
    couldnot: { label: "Couldn't look up", css: 'var(--f-couldnot)', glyph: 'cloud-off' },
    conflict: { label: 'Conflict', css: 'var(--f-conflict)', glyph: 'split' },
    known: { glyph: 'database' },
    cached: { glyph: 'history' },
};
const STATUS_CSS = {
    answered: 'var(--usage-answered)',
    after429: 'var(--usage-after-429)',
    gaveup: 'var(--usage-gave-up)',
    failed: 'var(--usage-failed)',
    det: 'var(--chart-1)',
};
const ANSWERS = [
    ['answered', 'answered'],
    ['after429', 'after a 429'],
    ['gaveup', 'gave up'],
    ['failed', 'failed'],
];
const GLYPH = {
    check: '<path d="M20 6 9 17l-5-5"/>',
    x: '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>',
    'cloud-off': '<path d="m2 2 20 20"/><path d="M5.782 5.782A7 7 0 0 0 9 19h8.5a4.5 4.5 0 0 0 1.307-.193"/><path d="M21.532 16.5A4.5 4.5 0 0 0 17.5 10h-1.79A7.008 7.008 0 0 0 10 5.07"/>',
    database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 21 19V5"/><path d="M3 12A9 3 0 0 0 21 12"/>',
    split: '<path d="M16 3h5v5"/><path d="M8 3H3v5"/><path d="M12 22v-8.3a4 4 0 0 0-1.172-2.872L3 3"/><path d="m15 9 6-6"/>',
    history: '<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/><path d="M12 7v5l4 2"/>',
    hourglass: '<path d="M5 22h14"/><path d="M5 2h14"/><path d="M17 22v-4.172a2 2 0 0 0-.586-1.414L12 12l-4.414 4.414A2 2 0 0 0 7 17.828V22"/><path d="M7 2v4.172a2 2 0 0 0 .586 1.414L12 12l4.414-4.414A2 2 0 0 0 17 6.172V2"/>',
};
const MARKS = {
    matched: '<path d="M-5.5 0.5 L-1.5 4.5 L5.5 -4" fill="none" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/>',
    notfound: '<path d="M-4.5 -4.5 L4.5 4.5 M4.5 -4.5 L-4.5 4.5" fill="none" stroke-width="2.4" stroke-linecap="round"/>',
    couldnot: '<path d="M0 -5.5 L0 1.5" fill="none" stroke-width="2.4" stroke-linecap="round"/><circle cy="5.2" r="1.4" stroke="none"/>',
};

const views = new WeakMap();

const fmt = n => Math.round(n).toLocaleString('en-US');
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const easeIO = u => (u < 0.5 ? 2 * u * u : 1 - Math.pow(-2 * u + 2, 2) / 2);
const escape = s => String(s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]);
const plural = (n, one, many) => `${fmt(n)} ${n === 1 ? one : many}`;
const secs = ms => Math.max(1, Math.ceil(ms / 1000));

function per(ms) {
    if (ms % 60000 === 0) return ms === 60000 ? 'a minute' : `per ${ms / 60000} minutes`;
    return ms === 1000 ? 'a second' : `per ${ms / 1000} seconds`;
}

function glyph(name, size) {
    return `<svg width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" style="display:block">${GLYPH[name]}</svg>`;
}

function bez(p, u) {
    const v = 1 - u;
    return [
        v * v * v * p[0][0] + 3 * v * v * u * p[1][0] + 3 * v * u * u * p[2][0] + u * u * u * p[3][0],
        v * v * v * p[0][1] + 3 * v * v * u * p[1][1] + 3 * v * u * u * p[2][1] + u * u * u * p[3][1],
    ];
}

function spread(count, from, to, at) {
    return count <= 1 ? (from + to) / 2 : from + (at * (to - from)) / (count - 1);
}

function tileHtml(brand, size) {
    const w = Math.round(size * brand.pct);
    const img = (src, cls) => `<img class="${cls}" src="${src}" style="width:${w}px;height:${w}px" alt="">`;
    const imgs = brand.dark !== brand.light ? img(brand.light, 'lt') + img(brand.dark, 'dk') : img(brand.light, '');
    return `<span class="fl-tile" style="width:${size}px;height:${size}px;background:${brand.background};border-radius:${Math.round(size * 0.24)}px">${imgs}</span>`;
}

function svgTile(brand, size) {
    const w = size * brand.pct;
    const img = (src, cls) => `<image class="${cls}" href="${src}" x="${-w / 2}" y="${-w / 2}" width="${w}" height="${w}" preserveAspectRatio="xMidYMid meet"/>`;
    const art = brand.dark !== brand.light ? img(brand.light, 'lt') + img(brand.dark, 'dk') : img(brand.light, '');
    return `<rect x="${-size / 2}" y="${-size / 2}" width="${size}" height="${size}" rx="${size * 0.24}" style="fill:${brand.background}"/>${art}`;
}

function svgEl(name, cls) {
    const el = document.createElementNS(SVG_NS, name);
    el.setAttribute('class', cls);
    return el;
}

class Model {
    constructor(keyOf) {
        this.keyOf = keyOf;
        this.titles = new Map();
        this.active = new Set();
        this.started = 0;
        this.c = { matched: 0, notfound: 0, couldnot: 0, conflict: 0, known: 0, cached: 0 };
        this.svc = {};
        this.edges = new Map();
        this.t = 0;
        this.running = true;
        this.sourceEnded = false;
        this.page = 0;
        this.reading = null;
    }

    node(key) {
        let s = this.svc[key];
        if (!s) {
            s = { answered: 0, after429: 0, gaveup: 0, failed: 0, inflight: 0, ema: 0, det: 0, detInflight: 0, blockedUntil: 0, busy: 0, pacing: false, pacedUntil: 0, pacedRequests: 0, pacedPer: 0 };
            this.svc[key] = s;
        }
        return s;
    }

    edge(a, b, kind) {
        const key = `${a}>${b}`;
        let e = this.edges.get(key);
        if (!e) {
            e = { key, a, b, n: 0, kind, last: -1e9 };
            this.edges.set(key, e);
        }
        e.n++;
        e.last = this.t;
        return e;
    }

    seed(snapshot) {
        this.t = snapshot.elapsedMs;
        this.running = !snapshot.ended;
        const f = snapshot.fates;
        this.started = f.started;
        this.reading = snapshot.reading;
        this.page = snapshot.pagesRead ?? 0;
        this.sourceEnded = !!snapshot.sourceEnded;
        this.c = {
            matched: f.matched - f.known,
            notfound: f.notFound - f.earlier + f.listedWithoutIds,
            couldnot: f.couldNotLookUp,
            conflict: f.conflicted,
            known: f.known,
            cached: f.earlier,
        };
        for (const n of snapshot.nodes) {
            const key = this.keyOf(n.service, n.stage);
            const s = this.node(key);
            if (n.stage === 0) {
                s.answered = n.answered;
                s.after429 = n.afterRateLimit;
                s.gaveup = n.gaveUp;
                s.failed = n.failed;
                s.inflight = n.waiting;
            } else {
                s.det = n.answered + n.afterRateLimit + n.gaveUp + n.failed;
                s.detInflight = n.waiting;
            }
            s.ema = n.meanMs;
        }
        for (const t of snapshot.titles) {
            const at = t.at ? this.keyOf(t.at.service, t.at.stage) : null;
            const last = t.lastIds != null ? this.keyOf(t.lastIds, 0) : null;
            const lastDet = t.lastDetails != null ? this.keyOf(t.lastDetails, 1) : null;
            this.titles.set(t.title, {
                id: t.title,
                name: t.name,
                year: t.year,
                state: at ? (at.startsWith('d:') ? 'details' : 'asking') : 'starting',
                svc: at ? at.replace('d:', '') : null,
                path: [],
                prev: lastDet ?? last ?? 'src',
                last,
                lastDet,
                fate: null,
                known: false,
                earlier: false,
                done: false,
            });
            this.active.add(t.title);
            if (at) this.edge(last ?? 'src', at, 'hop');
        }
    }

    apply(step) {
        this.t = step.atMs;
        const t = step.title >= 0 ? this.titles.get(step.title) : null;
        switch (KIND[step.kind]) {
            case 'started':
                this.titles.set(step.title, {
                    id: step.title,
                    name: step.name ?? '',
                    year: step.year || null,
                    state: 'starting',
                    svc: null,
                    path: [],
                    prev: 'src',
                    last: null,
                    lastDet: null,
                    fate: null,
                    known: false,
                    earlier: false,
                    done: false,
                });
                this.active.add(step.title);
                this.started++;
                return { started: true };
            case 'listed':
                this.c.notfound++;
                return null;
            case 'sourceEnded':
                this.sourceEnded = true;
                return null;
            case 'page':
                this.page++;
                return null;
            case 'known':
                if (t) t.known = true;
                return null;
            case 'earlier':
                if (t) t.earlier = true;
                return null;
            case 'path':
                if (t) t.prev = 'src';
                return null;
            case 'asked':
                return t ? this.asked(t, step) : null;
            case 'answered':
                return t ? this.answered(t, step) : null;
            case 'gaveUp':
                return t ? this.gaveUp(t, step) : null;
            case 'settled':
                return t ? this.settled(t, FATE_KEY[step.fate]) : null;
            default:
                return null;
        }
    }

    asked(t, step) {
        const key = this.keyOf(step.service, step.stage);
        const s = this.node(key);
        const from = step.stage === 1 ? (t.last ?? 'src') : t.prev;
        if (step.stage === 0) {
            s.inflight++;
            t.state = 'asking';
        } else {
            s.detInflight++;
            t.state = 'details';
        }
        t.svc = key.replace('d:', '');
        t.path.push(key);
        this.edge(from, key, 'hop');
        return { from, to: key, kind: 'hop' };
    }

    answered(t, step) {
        const key = this.keyOf(step.service, step.stage);
        const s = this.node(key);
        const outcome = OUTCOME[step.outcome];
        s.ema = s.ema ? s.ema * 0.9 + step.ms * 0.1 : step.ms;
        t.prev = key;
        if (step.stage === 0) {
            s.inflight = Math.max(0, s.inflight - 1);
            s[outcome]++;
            if (outcome === 'answered' || outcome === 'after429') t.last = key;
        } else {
            s.detInflight = Math.max(0, s.detInflight - 1);
            s.det++;
            if (outcome === 'answered' || outcome === 'after429') t.lastDet = key;
        }
        if (!t.done) t.state = 'thinking';
        return { flash: key, status: step.stage === 0 ? outcome : 'det' };
    }

    gaveUp(t, step) {
        const key = this.keyOf(step.service, step.stage);
        const s = this.node(key);
        if (step.stage === 0) s.gaveup++;
        else s.det++;
        const from = step.stage === 1 ? (t.last ?? 'src') : t.prev;
        t.path.push(key);
        t.prev = key;
        if (step.stage === 0) t.last = key;
        this.edge(from, key, 'hop');
        return { flash: key, status: 'gaveup', from, to: key, kind: 'hop' };
    }

    settled(t, fate) {
        t.fate = fate;
        const earlier = fate === 'notfound' && t.earlier;
        const tick = earlier || (fate === 'matched' && t.known && !t.path.length);
        if (earlier) this.c.cached++;
        else if (t.known) this.c.known++;
        else this.c[fate]++;
        const from = fate === 'matched' ? (t.lastDet ?? t.last ?? 'src') : (t.last ?? 'src');
        const to = FATE_NODE[fate];
        this.edge(from, to, 'fate-' + fate);
        this.finish(t);
        return { from, to, kind: 'fate', fate, tick, finished: true };
    }

    finish(t) {
        t.done = true;
        t.state = 'done';
        this.active.delete(t.id);
    }

    end() {
        this.running = false;
        for (const id of this.active) {
            const t = this.titles.get(id);
            if (t) t.done = true;
        }
        this.active.clear();
        for (const s of Object.values(this.svc)) {
            s.inflight = 0;
            s.detInflight = 0;
        }
    }
}

class Slots {
    constructor(n) {
        this.s = Array.from({ length: n }, () => ({ id: null, doneAt: 0, freeAt: 0 }));
        this.of = new Map();
    }

    take(id, now) {
        const z = this.s.find(x => x.id === null && now >= x.freeAt);
        if (!z) return;
        z.id = id;
        z.doneAt = 0;
        this.of.set(id, this.s.indexOf(z));
    }

    release(id, now) {
        const i = this.of.get(id);
        if (i != null) this.s[i].doneAt = now;
    }

    sweep(now, model) {
        for (const z of this.s) {
            if (z.id !== null && z.doneAt && now >= z.doneAt + LINGER) {
                this.of.delete(z.id);
                z.id = null;
                z.doneAt = 0;
                z.freeAt = now + 120;
            }
        }
        if (!this.s.some(z => z.id === null && now >= z.freeAt)) return;
        const act = [...model.active];
        for (let i = act.length - 1; i >= 0; i--) {
            if (this.of.has(act[i])) continue;
            this.take(act[i], now);
            if (!this.s.some(z => z.id === null && now >= z.freeAt)) break;
        }
    }

    fill(model) {
        for (const z of this.s) {
            z.id = null;
            z.doneAt = 0;
            z.freeAt = 0;
        }
        this.of.clear();
        [...model.active].slice(-this.s.length).forEach(id => this.take(id, 0));
    }
}

class View {
    static count = 0;

    constructor(host, layout, snapshot) {
        this.uid = ++View.count;
        this.host = host;
        this.layout = layout;
        this.brand = {};
        this.keys = {};
        for (const n of layout.nodes) {
            this.brand[n.key] = n.brand;
            this.keys[`${n.service}:0`] = n.key;
        }
        for (const n of layout.details) {
            this.brand['d:' + n.key] = n.brand;
            this.keys[`${n.service}:1`] = 'd:' + n.key;
        }
        this.inMemory = new Set(layout.nodes.filter(n => n.inMemory).map(n => n.key));
        this.model = new Model((service, stage) => this.keys[`${service}:${stage}`] ?? `${stage === 1 ? 'd:' : ''}svc${service}`);
        this.model.seed(snapshot);
        this.q = [];
        this.qi = 0;
        this.ticks = new Set();
        this.anchor = performance.now() - snapshot.elapsedMs;
        this.lastSeq = snapshot.cursorSeq;
        this.hidden = document.hidden;
        this.reanchor = false;
        this.place(host.clientWidth);
        this.build();
        this.slots.fill(this.model);
        this.onVisibility = () => this.visibility();
        document.addEventListener('visibilitychange', this.onVisibility);
        this.resize = new ResizeObserver(() => {
            this.fit();
            clearTimeout(this.reflowTimer);
            this.reflowTimer = setTimeout(() => this.reflow(), 150);
        });
        this.resize.observe(this.host);
        this.loop = now => {
            if (!this.host.isConnected) {
                this.dispose();
                return;
            }
            this.raf = requestAnimationFrame(this.loop);
            this.frame(now);
        };
        this.raf = requestAnimationFrame(this.loop);
    }

    place(width) {
        const ids = this.layout.nodes;
        const details = this.layout.details;
        const columns = Math.max(1, ...ids.map(n => n.column));
        const byColumn = Array.from({ length: columns }, (_, c) => ids.filter(n => n.column === c + 1));
        const detailCols = details.length ? Math.ceil(details.length / DETAIL_ROWS) : 0;
        const detailRows = detailCols ? Math.ceil(details.length / detailCols) : 0;
        const rows = Math.max(3, detailRows, ...byColumn.map(c => c.length));
        const H = Math.max(MIN_H, TOP + BOTTOM + (rows - 1) * NODE_GAP);
        const gaps = columns + detailCols + 1;
        const W = Math.max(MIN_W, DB_X + gaps * COL_GAP + 80, Math.floor(width || 0));
        const outX = W - 80;
        const step = (outX - DB_X) / gaps;
        const idX = c => DB_X + (c + 1) * step;
        const detailX = c => DB_X + (columns + c + 1) * step;
        const mid = H / 2;
        const dbY = Math.round(mid - 15);
        const L = { src: [LIST_X, dbY], db: [DB_X, dbY], sink: [outX, Math.round(mid - 108)], cl: [outX, Math.round(mid + 2)], nf: [outX, Math.round(mid + 112)], pos: {} };
        byColumn.forEach((inColumn, c) => {
            inColumn.forEach((n, i) => {
                L.pos[n.key] = [Math.round(idX(c)), Math.round(spread(inColumn.length, TOP, H - BOTTOM, i))];
            });
        });
        details.forEach((n, i) => {
            const c = Math.floor(i / detailRows);
            const inColumn = Math.min(detailRows, details.length - c * detailRows);
            const span = Math.min(H - BOTTOM - TOP, (inColumn - 1) * NODE_GAP * 1.25);
            L.pos['d:' + n.key] = [Math.round(detailX(c)), Math.round(spread(inColumn, mid - span / 2, mid + span / 2, i % detailRows))];
        });
        const byX = new Map();
        for (const [key, [x, y]] of Object.entries(L.pos)) byX.set(x, [...(byX.get(x) ?? []), [key, y]]);
        this.cols = [...byX.entries()];
        this.W = W;
        this.H = H;
        this.step = step;
        this.idX = Math.round(idX(0));
        this.detailX = detailCols ? Math.round(detailX(0)) : null;
        this.slotN = Math.max(1, Math.floor((H - SLOT_TOP - 6) / SLOT_H));
        this.L = L;
    }

    fit() {
        this.inner.style.zoom = String(Math.min(1, this.host.clientWidth / this.W || 1));
    }

    reflow() {
        if (!this.host.isConnected) return;
        const before = this.W;
        this.place(this.host.clientWidth);
        if (this.W === before) {
            this.fit();
            return;
        }
        this.build();
        this.slots.fill(this.model);
    }

    pos(k) {
        if (k === 'src') return this.L.db;
        if (k === 'sink' || k === 'nf' || k === 'cl') return this.L[k];
        return this.L.pos[k] ?? this.L.db;
    }

    rad(k) {
        return k === 'sink' || k === 'nf' || k === 'cl' ? 20 : NODE_R + 11;
    }

    geo(a, b, from) {
        const pa = from || this.pos(a);
        const pb = this.pos(b);
        const ra = from ? 0 : this.rad(a);
        const rb = this.rad(b);
        const p0 = [pa[0] + ra, pa[1]];
        const p3 = [pb[0] - rb, pb[1]];
        const dx = Math.max(40, (p3[0] - p0[0]) / 2);
        return [p0, [p0[0] + dx, p0[1]], [p3[0] - dx, p3[1]], p3];
    }

    pathD(p) {
        return `M${p[0][0]},${p[0][1]} C${p[1][0]},${p[1][1]} ${p[2][0]},${p[2][1]} ${p[3][0]},${p[3][1]}`;
    }

    used(key) {
        const v = this.model.node(key);
        return key.startsWith('d:') ? v.det + v.detInflight > 0 : v.answered + v.after429 + v.gaveup + v.failed + v.inflight > 0;
    }

    line(a, b) {
        return this.route(a, b) ?? this.pathD(this.geo(a, b));
    }

    route(a, b) {
        if (a === 'src' ? b in OUTCOMES : (b === 'cl' || b === 'nf') && !a.startsWith('d:')) return this.lane(a, b);
        return a !== b ? this.gatePath(a, b) : null;
    }

    lane(a, b) {
        const [x0, y0] = this.pos(a);
        const [x3, y3] = this.pos(b);
        const [ex, fy] = this.dock(b);
        const s = Math.sign(fy - y0);
        const r = 10;
        const side = this.blocked(a);
        const gx = side ? x0 + this.step / 2 + (Math.round(y0 / NODE_GAP) % 3) * 7 - 7 : x0;
        const out = side ? `M${x0 + this.rad(a)},${y0} H${gx - r} Q${gx},${y0} ${gx},${y0 + r}` : `M${x0},${y0 + s * this.rad(a)}`;
        const run = `${out} V${fy - s * r} Q${gx},${fy} ${gx + r},${fy} H${ex - r} Q${ex},${fy} ${ex},${fy - s * r}`;
        return b === 'cl' ? `${run} V${y3 + r} Q${ex},${y3} ${ex + r},${y3} H${x3 - this.rad(b)}` : `${run} V${y3 + s * this.rad(b)}`;
    }

    blocked(a) {
        const [x0, y0] = this.pos(a);
        return this.cols.some(([x, ys]) => x === x0 && ys.some(([, y]) => y > y0));
    }

    dock(b) {
        const [x3] = this.pos(b);
        if (b === 'cl') return [x3 - 58, this.H - 15];
        return [x3, b === 'sink' ? 6 : this.H - 6];
    }

    gatePath(a, b) {
        const g = this.geo(a, b);
        const [x0, y0] = g[0];
        const [x3, y3] = g[3];
        const curveY = x => {
            let lo = 0;
            let hi = 1;
            for (let i = 0; i < 24; i++) {
                const mid = (lo + hi) / 2;
                if (bez(g, mid)[0] < x) lo = mid;
                else hi = mid;
            }
            return bez(g, (lo + hi) / 2)[1];
        };
        const points = [[x0, y0]];
        for (const [x, ys] of this.cols) {
            if (x <= x0 + 30 || x >= x3 - 30) continue;
            const boxes = ys.filter(([key]) => this.used(key)).map(([, y]) => [y - 36, y + 76]);
            const want = curveY(x);
            const inside = v => boxes.find(([top, bottom]) => v > top && v < bottom);
            if (!inside(want)) continue;
            let up = want;
            while (inside(up)) up = inside(up)[0] - 4;
            let down = want;
            while (inside(down)) down = inside(down)[1] + 4;
            const upFits = up > 14;
            const downFits = down < this.H - 12;
            points.push([x, upFits && (!downFits || want - up <= down - want) ? up : down]);
        }
        if (points.length === 1) return null;
        points.push([x3, y3]);
        const last = points.length - 1;
        const tangent = i => {
            if (i === 0) return [points[1][0] - points[0][0], 0];
            if (i === last) return [points[last][0] - points[last - 1][0], 0];
            return [(points[i + 1][0] - points[i - 1][0]) / 2, (points[i + 1][1] - points[i - 1][1]) / 2];
        };
        let d = `M${x0.toFixed(1)},${y0.toFixed(1)}`;
        for (let i = 0; i < last; i++) {
            const [xa, ya] = points[i];
            const [xb, yb] = points[i + 1];
            const [ta, ua] = tangent(i);
            const [tb, ub] = tangent(i + 1);
            d += ` C${(xa + ta / 3).toFixed(1)},${(ya + ua / 3).toFixed(1)} ${(xb - tb / 3).toFixed(1)},${(yb - ub / 3).toFixed(1)} ${xb.toFixed(1)},${yb.toFixed(1)}`;
        }
        return d;
    }

    reroute() {
        const signature = Object.keys(this.nd)
            .filter(key => this.used(key))
            .join(',');
        if (signature === this.usedSignature) return;
        this.usedSignature = signature;
        for (const { el, e } of this.E.values()) el.setAttribute('d', this.line(e.a, e.b));
    }

    build() {
        const L = this.L;
        const [sx] = L.src;
        const [dbx, dby] = L.db;
        const detailsX = this.detailX;
        this.host.innerHTML = `<div class="fl-fit"><div class="fl-view">
            <div class="fl-list"><div class="fl-lh">In flight <b class="fl-n">0</b><span class="fl-more"></span></div>
            ${Array.from({ length: this.slotN }, (_, i) => `<div class="fl-slot is-empty" data-i="${i}" style="top:${SLOT_TOP + i * SLOT_H}px"><span class="st"></span><span class="nm"></span><span class="yr"></span></div>`).join('')}</div>
            <svg class="fl-svg" viewBox="0 0 ${this.W} ${this.H}" width="${this.W}" height="${this.H}">
                <text class="fl-cap" x="${sx - 12}" y="26">List</text><text class="fl-cap" x="${this.idX - 40}" y="26">Services</text>${detailsX ? `<text class="fl-cap" x="${detailsX - 28}" y="26">Details</text>` : ''}<text class="fl-cap" x="${L.sink[0] - 10}" y="26" text-anchor="end">Outcome</text>
                <defs><marker id="fla${this.uid}" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" markerUnits="userSpaceOnUse" orient="auto"><path d="M0.5,0.5 L7.5,4 L0.5,7.5 Z" class="fl-arrow"/></marker></defs>
                <g class="g-edges"></g><g class="g-wait"><path class="fl-entry" d="M${sx + NODE_R + 9},${dby} L${dbx - NODE_R - 9},${dby}" marker-end="url(#fla${this.uid})"/><text class="fl-entry-cap" x="${sx}" y="${dby - NODE_R - 12}"></text></g><path class="fl-hl"/><g class="g-parts"></g><g class="g-nodes"></g>
            </svg><div class="fl-tip" hidden></div></div></div>`;
        this.inner = this.host.querySelector('.fl-fit');
        this.inner.style.width = `${this.W}px`;
        this.viewEl = this.host.querySelector('.fl-view');
        this.viewEl.style.height = `${this.H}px`;
        this.fit();
        const svg = this.host.querySelector('svg');
        this.gEdges = svg.querySelector('.g-edges');
        this.gWait = svg.querySelector('.g-wait');
        this.gParts = svg.querySelector('.g-parts');
        this.hl = svg.querySelector('.fl-hl');
        this.tip = this.host.querySelector('.fl-tip');
        this.entry = { path: svg.querySelector('.fl-entry'), cap: svg.querySelector('.fl-entry-cap') };
        const out = [];
        for (const n of this.layout.nodes) out.push(this.nodeHtml(n.key, n.brand, this.pos(n.key), n.brand.name));
        for (const n of this.layout.details) out.push(this.nodeHtml('d:' + n.key, n.brand, this.pos('d:' + n.key), n.brand.name + ' details'));
        out.push(this.dbHtml(), this.srcHtml());
        for (const [k, { fate: f }] of Object.entries(OUTCOMES)) {
            out.push(`<g class="fl-fate" data-k="${k}" transform="translate(${L[k][0]},${L[k][1]})" style="stroke:${FATE[f].css};fill:${FATE[f].css}"><circle r="16" style="fill:color-mix(in oklch, ${FATE[f].css} 18%, var(--card))" stroke-width="2"/>${MARKS[f]}<text class="fl-fate-name" y="34">${FATE[f].label}</text><text class="fl-fate-cap" y="48"></text></g>`);
        }
        svg.querySelector('.g-nodes').innerHTML = out.join('');
        this.nd = {};
        for (const g of svg.querySelectorAll('.fl-nd')) {
            this.nd[g.dataset.k] = {
                g,
                stat: g.querySelector('.nd-stat'),
                pace: g.querySelector('.nd-pace'),
                halo: g.querySelector('.nd-halo'),
                wait: g.querySelector('.nd-wait'),
                flash: 0,
                flashColor: '',
            };
        }
        this.dbG = svg.querySelector('.fl-db');
        this.dbSegs = [...svg.querySelectorAll('.db-seg')];
        this.dbStat = svg.querySelector('.db-stat');
        this.srcG = svg.querySelector('.fl-src');
        this.srcStat = svg.querySelector('.src-stat');
        this.fates = {};
        for (const g of svg.querySelectorAll('.fl-fate')) this.fates[g.dataset.k] = { g, ring: g.querySelector('circle'), cap: g.querySelector('.fl-fate-cap') };
        this.tipKey = null;
        const hoverable = [...Object.values(this.nd), ...Object.values(this.fates)].map(({ g }) => [g, g.dataset.k]);
        for (const [g, key] of [...hoverable, [this.dbG, 'src'], [this.srcG, 'source']]) {
            g.addEventListener('mouseenter', () => (this.tipKey = key));
            g.addEventListener('mouseleave', () => (this.tipKey = null));
        }
        this.waitNow = new Map();
        this.waitEls = new Map();
        this.E = new Map();
        this.parts = [];
        this.pool = [];
        this.entryEnd = new Map();
        this.slots = new Slots(this.slotN);
        this.slotEls = [...this.host.querySelectorAll('.fl-slot')];
        for (const el of this.slotEls) {
            el.addEventListener('mouseenter', () => {
                this.hover = this.slots.s[+el.dataset.i].id;
            });
            el.addEventListener('mouseleave', () => {
                this.hover = null;
            });
        }
        this.listN = this.host.querySelector('.fl-n');
        this.listMore = this.host.querySelector('.fl-more');
    }

    nodeHtml(k, brand, [x, y], label) {
        return `<g class="fl-nd" data-k="${k}" transform="translate(${x},${y})">
            <circle class="nd-halo" r="${NODE_R + 9}" style="fill:${brand.color}"/><circle class="nd-wait" r="${NODE_R + 5}" transform="rotate(-90)" stroke-dasharray="0 ${WAIT_RING}"/><circle class="nd-bg" r="${NODE_R}"/><circle class="nd-track" r="${NODE_R}"/>
            ${svgTile(brand, 26)}
            <text class="nd-name" y="${NODE_R + 19}">${escape(label)}</text><text class="nd-stat" y="${NODE_R + 33}"></text><text class="nd-pace" y="${NODE_R + 47}"></text></g>`;
    }

    dbHtml() {
        const [x, y] = this.L.db;
        return `<g class="fl-db" transform="translate(${x},${y})"><circle class="nd-bg" r="${NODE_R}"/><circle class="nd-track" r="${NODE_R}"/>
            ${['var(--f-known)', 'var(--f-cached)', 'var(--f-new)'].map(c => `<circle class="db-seg" r="${NODE_R}" style="stroke:${c}" stroke-dasharray="0 ${RING}" transform="rotate(-90)"/>`).join('')}
            ${svgTile(this.layout.sammlarr, 26)}<text class="nd-name" y="${NODE_R + 19}">Sammlarr</text><text class="nd-stat db-stat" y="${NODE_R + 33}"></text></g>`;
    }

    srcHtml() {
        const [x, y] = this.L.src;
        return `<g class="fl-src" transform="translate(${x},${y})"><circle class="nd-orbit" r="${NODE_R + 5}"/><circle class="nd-bg" r="${NODE_R}"/><circle class="nd-track" r="${NODE_R}"/>
            ${svgTile(this.layout.source, 26)}<text class="nd-name" y="${NODE_R + 19}">${escape(this.layout.source.name)} list</text><text class="nd-stat src-stat" y="${NODE_R + 33}"></text></g>`;
    }

    edgeEl(e) {
        let x = this.E.get(e.key);
        if (x) return x;
        const el = svgEl('path', e.kind === 'hop' ? 'fl-ed' : `fl-ed is-${e.kind}`);
        el.setAttribute('d', this.line(e.a, e.b));
        el.setAttribute('marker-end', `url(#fla${this.uid})`);
        this.gEdges.appendChild(el);
        x = { el, e };
        this.E.set(e.key, x);
        return x;
    }

    tick(to) {
        this.ticks.add(to);
    }

    flushTicks() {
        if (!REDUCED.matches) for (const to of this.ticks) this.fates[to].ring.animate([{ strokeWidth: '6px' }, { strokeWidth: '2px' }], { duration: 450, easing: 'ease-out' });
        this.ticks.clear();
    }

    along(path, from, spacing, count) {
        const len = path.getTotalLength();
        return Array.from({ length: count }, (_, i) => {
            const at = Math.max(0, len - from - i * spacing);
            const pt = path.getPointAtLength(at);
            const back = path.getPointAtLength(Math.max(0, at - 1));
            return [pt.x, pt.y, pt.x - back.x, pt.y - back.y];
        });
    }

    track(a, b) {
        const path = this.edgeEl(this.model.edges.get(`${a}>${b}`)).el;
        return { path, len: path.getTotalLength() };
    }

    waitMark(e, into, hot) {
        const all = into ? this.edgeWait.get(`${e.a}>${e.b}`) || 0 : 0;
        let w = this.waitEls.get(e.key);
        if (!w) {
            if (!all) return;
            const gate = svgEl('path', 'wt-gate');
            const parked = Array.from({ length: QUEUE }, () => {
                const dot = svgEl('circle', 'wt-park');
                dot.setAttribute('r', '3.4');
                return dot;
            });
            const more = svgEl('text', 'wt-more');
            this.gWait.append(gate, ...parked, more);
            w = { gate, parked, more };
            this.waitEls.set(e.key, w);
        }
        for (const el of [w.gate, ...w.parked, w.more]) el.classList.toggle('is-hot', hot);
        if (!all) {
            if (w.sig === '') return;
            w.parked.forEach(dot => (dot.style.display = 'none'));
            w.more.textContent = '';
            w.gate.style.display = 'none';
            w.sig = '';
            return;
        }
        const path = this.E.get(e.key).el;
        const blocked = into === 'blocked';
        const sig = `${all}|${blocked}|${path.getAttribute('d')}`;
        if (w.sig === sig) return;
        w.sig = sig;
        const spots = this.along(path, blocked ? 18 : 10, 7.5, QUEUE + 1);
        w.parked.forEach((dot, i) => {
            dot.setAttribute('cx', spots[i][0].toFixed(1));
            dot.setAttribute('cy', spots[i][1].toFixed(1));
            dot.style.display = i < all ? '' : 'none';
            dot.style.fill = blocked ? 'var(--usage-after-429)' : 'var(--f-flight)';
        });
        w.gate.style.display = blocked ? '' : 'none';
        if (blocked) {
            const [gx, gy, tx, ty] = this.along(path, 10, 0, 1)[0];
            const tl = Math.hypot(tx, ty) || 1;
            const nx = -ty / tl;
            const ny = tx / tl;
            w.gate.setAttribute('d', `M${(gx - nx * 7).toFixed(1)},${(gy - ny * 7).toFixed(1)} L${(gx + nx * 7).toFixed(1)},${(gy + ny * 7).toFixed(1)}`);
        }
        const [mx, my] = spots[QUEUE];
        w.more.setAttribute('x', (mx + 3).toFixed(1));
        w.more.setAttribute('y', (my + 3.5).toFixed(1));
        w.more.textContent = all > QUEUE ? `+${fmt(all - QUEUE)}` : '';
        w.more.style.fill = blocked ? 'var(--usage-after-429-text)' : 'var(--f-flight)';
    }

    paintTip(sim, reading) {
        const k = this.tipKey;
        this.tip.hidden = !k;
        if (!k) return;
        const m = this.model;
        if (k === 'source') {
            const state = reading ? `<div class="tw">Sending page ${fmt(m.reading)}</div>` : m.sourceEnded ? '<div>Whole list read</div>' : '';
            this.tipHtml(`<b>${escape(this.layout.source.name)} list</b><div>${escape(this.layout.sourceName)}</div><div>${plural(m.page, 'page', 'pages')} read · ${fmt(m.started)} titles so far</div>${state}`);
        } else if (k === 'src') {
            this.tipHtml(`<b>Sammlarr</b><div>${fmt(m.c.known)} already known · ${fmt(m.c.cached)} not found earlier · ${fmt(Math.max(0, m.started - m.c.known - m.c.cached))} new</div>`);
        } else if (OUTCOMES[k]) {
            const { fate, meaning, also } = OUTCOMES[k];
            const more = also && m.c[also.count] ? `<div class="tx"><b>+ ${fmt(m.c[also.count])} ${also.label}</b> ${escape(this.layout.meanings[also.meaning])}</div>` : '';
            this.tipHtml(`<b>${FATE[fate].label}</b><div>${escape(this.layout.meanings[meaning])}</div>${more}`);
        } else {
            this.tipHtml(this.serviceTip(k, sim));
        }
        const [x, y] = k === 'source' ? this.L.src : this.pos(k);
        this.tip.style.left = `${x + 260 > this.W ? x - 34 - 240 : x + 34}px`;
        this.tip.style.top = `${y - 30}px`;
    }

    tipHtml(html) {
        if (this.tipShown === html) return;
        this.tipShown = html;
        this.tip.innerHTML = html;
    }

    serviceTip(k, sim) {
        const det = k.startsWith('d:');
        const v = this.model.node(k);
        const done = det ? v.det : v.answered + v.after429 + v.gaveup + v.failed;
        const waiting = det ? v.detInflight : v.inflight;
        const name = `<b>${escape(this.brand[k].name)}${det ? ' details' : ''}</b>`;
        if (done + waiting === 0) return `${name}<div>Not asked yet</div>`;
        const split = det
            ? `<div>${fmt(v.det)} answered</div>`
            : `<div class="ts">${ANSWERS.filter(([key]) => v[key])
                  .map(([key, label]) => `<span><i style="background:${STATUS_CSS[key]}"></i>${fmt(v[key])} ${label}</span>`)
                  .join('')}</div>`;
        const blocked = this.model.running && v.blockedUntil > sim;
        const pace = this.pacing(v, sim) ? `<div class="tx">Paced at ${fmt(v.pacedRequests)} ${per(v.pacedPer)} · ${this.nextSend(v, sim)}</div>` : '';
        return `${name}<div>${fmt(done + waiting)} asked${v.ema ? ` · ${Math.round(v.ema)} ms on average` : ''}</div>${done ? split : ''}${waiting ? `<div class="tw">${fmt(waiting)} waiting for an answer</div>` : ''}${pace}${blocked ? `<div class="tb">Rate-limited (429): free again in ${secs(v.blockedUntil - sim)} s</div>` : ''}`;
    }

    pacing(v, sim) {
        return this.model.running && v.pacing && !(v.blockedUntil > sim);
    }

    nextSend(v, sim) {
        return v.pacedUntil > sim ? `next ${fmt(v.pacedRequests)} in ${secs(v.pacedUntil - sim)} s` : 'sending';
    }

    slotAnchor(id) {
        const i = this.slots.of.get(id);
        return i == null ? null : [262, SLOT_TOP + i * SLOT_H + 12];
    }

    push(batch) {
        const now = performance.now();
        const sim = this.simNow(now);
        for (const c of batch.capacity) {
            for (const key of [this.keys[`${c.service}:0`], this.keys[`${c.service}:1`]]) {
                if (!key) continue;
                const s = this.model.node(key);
                if (c.blockedUntilMs && c.blockedUntilMs !== s.blockedUntil && !(s.blockedUntil > sim)) s.blockedFrom = sim;
                s.blockedUntil = c.blockedUntilMs;
                s.busy = c.busy;
                if (c.pacedUntilMs && c.pacedUntilMs !== s.pacedUntil && !(s.pacedUntil > sim)) s.pacedFrom = sim;
                s.pacedUntil = c.pacedUntilMs;
                if (c.pacedUntilMs) {
                    s.pacing = true;
                    s.pacedRequests = c.pacedRequests;
                    s.pacedPer = c.pacedPerMs;
                }
            }
        }
        this.model.reading = batch.reading;
        const steps = batch.steps.filter(step => step.seq > this.lastSeq);
        if (steps.length) this.lastSeq = steps[steps.length - 1].seq;
        if (this.hidden) {
            this.drain();
            for (const step of steps) this.model.apply(step);
        } else {
            if (steps.length) {
                const late = now - (this.anchor + steps[0].atMs + DELAY);
                if (this.reanchor || late > LATE) {
                    this.anchor += late;
                    this.reanchor = false;
                }
            }
            for (const step of steps) this.q.push([this.anchor + step.atMs + DELAY, step]);
        }
        if (batch.ended) this.endAt = steps.length ? this.anchor + steps[steps.length - 1].atMs + DELAY : now;
    }

    drain() {
        while (this.qi < this.q.length) this.model.apply(this.q[this.qi++][1]);
        this.q = [];
        this.qi = 0;
        this.ticks = new Set();
    }

    visibility() {
        this.hidden = document.hidden;
        if (this.hidden) return;
        this.drain();
        for (const p of this.parts) {
            p.el.style.display = 'none';
            this.pool.push(p.el);
        }
        this.parts = [];
        this.slots.fill(this.model);
        this.reanchor = true;
    }

    frame(now) {
        while (this.qi < this.q.length && this.q[this.qi][0] <= now) {
            const step = this.q[this.qi++][1];
            this.onStep(step, now, this.model.apply(step));
        }
        this.flushTicks();
        if (this.qi > 2000) {
            this.q = this.q.slice(this.qi);
            this.qi = 0;
        }
        if (this.endAt && now >= this.endAt && this.model.running) this.model.end();
        this.render(now);
    }

    simNow(now) {
        return this.model.running ? Math.max(this.model.t, now - this.anchor - DELAY) : this.model.t;
    }

    onStep(step, now, fx) {
        if (fx?.started) {
            this.slots.take(step.title, now);
            if (this.entryAt !== now) this.spawn(this.geo('src', 'src', [this.L.src[0] + NODE_R + 9, this.L.src[1]]), 'var(--muted-foreground)', now, 240);
            this.entryAt = now;
            this.entryEnd.set(step.title, now + 240);
            return;
        }
        if (!fx) return;
        if (fx.flash) {
            const node = this.nd[fx.flash];
            if (node) {
                node.flash = now;
                node.flashColor = STATUS_CSS[fx.status] || STATUS_CSS.answered;
            }
        }
        if (fx.tick) this.tick(fx.to);
        if (fx.to) {
            const t0 = fx.from === 'src' ? Math.max(now, this.entryEnd.get(step.title) || now) : now;
            this.spawn(this.track(fx.from, fx.to), fx.kind === 'hop' ? 'var(--foreground)' : FATE[fx.fate].css, t0, 300);
        }
        if (fx.finished) {
            this.slots.release(step.title, now);
            this.entryEnd.delete(step.title);
        }
    }

    spawn(geo, color, t0, dur) {
        if (REDUCED.matches || this.parts.length > MAX_DOTS) return;
        let el = this.pool.pop();
        if (!el) {
            el = document.createElementNS(SVG_NS, 'circle');
            el.setAttribute('r', '3.6');
            this.gParts.appendChild(el);
        }
        el.style.fill = color;
        el.style.display = 'none';
        this.parts.push({ el, geo, t0, dur });
    }

    render(now) {
        const m = this.model;
        this.slots.sweep(now, m);
        const sim = this.simNow(now);
        for (const [k, n] of Object.entries(this.nd)) {
            const det = k.startsWith('d:');
            const v = m.node(k);
            const done = det ? v.det : v.answered + v.after429 + v.gaveup + v.failed;
            const w = det ? v.detInflight : v.inflight;
            const asked = done + w;
            const blocked = m.running && v.blockedUntil > sim;
            if (!w) v.pacing = false;
            const pacing = this.pacing(v, sim);
            const paced = pacing && v.pacedUntil > sim;
            n.g.classList.toggle('is-idle', asked === 0);
            n.stat.textContent = asked === 0 ? '' : this.inMemory.has(k) ? `${fmt(asked)} lookups · in memory` : `${fmt(asked)} asked · ${Math.round(v.ema)} ms`;
            n.pace.textContent = pacing ? `${fmt(w)} queued · ${this.nextSend(v, sim)}` : '';
            const fl = n.flash ? Math.max(0, 1 - (now - n.flash) / 380) : 0;
            const busy = blocked ? 0 : v.busy;
            n.halo.style.opacity = String(Math.min(0.42, (pacing ? 0.06 : busy * 0.3) + fl * 0.35));
            n.halo.style.fill = fl > 0 ? n.flashColor : this.brand[k].color;
            this.waitNow.set(k, !m.running ? '' : blocked ? 'blocked' : w > 0 ? 'wait' : '');
            const until = paced ? v.pacedUntil : v.blockedUntil;
            const span = Math.max(1, until - ((paced ? v.pacedFrom : v.blockedFrom) ?? sim));
            const left = REDUCED.matches ? Math.ceil((until - sim) / 1000) * 1000 : until - sim;
            const share = blocked || paced ? clamp(left / span, 0, 1) : 0;
            n.wait.classList.toggle('is-paced', paced);
            n.wait.setAttribute('stroke-dasharray', `${(share * WAIT_RING).toFixed(1)} ${WAIT_RING}`);
            n.wait.style.display = share > 0 ? 'inline' : 'none';
        }
        const reading = m.running && m.reading != null;
        this.entry.cap.textContent = reading ? `reading page ${fmt(m.reading)}` : '';
        this.srcG.classList.toggle('is-reading', reading);
        this.srcStat.textContent = m.page ? `${plural(m.page, 'page', 'pages')} · ${fmt(m.started)} titles` : 'first page';
        const vals = [m.c.known, m.c.cached, Math.max(0, m.started - m.c.known - m.c.cached)];
        const dbTotal = vals.reduce((a, b) => a + b, 0);
        let off = 0;
        this.dbSegs.forEach((seg, i) => {
            const len = dbTotal ? (RING * vals[i]) / dbTotal : 0;
            seg.setAttribute('stroke-dasharray', `${len} ${RING}`);
            seg.setAttribute('stroke-dashoffset', `${-off}`);
            off += len;
        });
        this.dbStat.textContent = `${fmt(vals[2])} new · ${fmt(m.c.known)} known · ${fmt(m.c.cached)} earlier`;
        this.fates.sink.cap.textContent = fmt(m.c.matched) + (m.c.known ? ` + ${fmt(m.c.known)} known` : '');
        this.fates.nf.cap.textContent = fmt(m.c.notfound) + (m.c.cached ? ` + ${fmt(m.c.cached)} earlier` : '');
        this.fates.cl.cap.textContent = fmt(m.c.couldnot) + (m.c.conflict ? ` + ${fmt(m.c.conflict)} in conflict` : '');
        this.edgeWait = new Map();
        for (const id of m.active) {
            const t = m.titles.get(id);
            if (!t || !t.svc || (t.state !== 'asking' && t.state !== 'details')) continue;
            const key = (t.state === 'details' ? 'd:' : '') + t.svc;
            const from = t.state === 'details' ? (t.last ?? 'src') : t.prev;
            const edgeKey = `${from}>${key}`;
            this.edgeWait.set(edgeKey, (this.edgeWait.get(edgeKey) || 0) + 1);
        }
        this.reroute();
        const focusKey = this.tipKey;
        this.viewEl.classList.toggle('fl-focus', !!focusKey);
        const hotNodes = new Set(focusKey ? [focusKey] : []);
        for (const e of m.edges.values()) {
            const hot = !!focusKey && (e.a === focusKey || e.b === focusKey);
            if (hot) {
                hotNodes.add(e.a);
                hotNodes.add(e.b);
            }
            const x = this.edgeEl(e);
            x.el.classList.toggle('is-hot', hot);
            this.waitMark(e, e.kind === 'hop' ? this.waitNow.get(e.b) || '' : '', hot);
            x.el.setAttribute('stroke-width', (1 + 2.4 * Math.log10(1 + e.n)).toFixed(2));
            const recent = Math.max(0, 1 - (sim - e.last) / FRESH);
            const waiting = (this.edgeWait.get(`${e.a}>${e.b}`) || 0) > 0;
            x.el.style.opacity = (m.running ? Math.max(waiting ? 0.7 : 0, 0.02 + 0.9 * recent) : 0.5).toFixed(2);
        }
        const listHot = focusKey === 'source' || focusKey === 'src';
        for (const [k, n] of Object.entries(this.nd)) n.g.classList.toggle('is-hot', hotNodes.has(k));
        for (const [k, f] of Object.entries(this.fates)) f.g.classList.toggle('is-hot', hotNodes.has(k));
        this.dbG.classList.toggle('is-hot', hotNodes.has('src') || focusKey === 'source');
        this.srcG.classList.toggle('is-hot', listHot);
        this.entry.path.classList.toggle('is-hot', listHot);
        this.entry.cap.classList.toggle('is-hot', listHot);
        for (let i = this.parts.length - 1; i >= 0; i--) {
            const p = this.parts[i];
            const u = (now - p.t0) / p.dur;
            if (u < 0) continue;
            if (u >= 1) {
                p.el.style.display = 'none';
                this.pool.push(p.el);
                this.parts.splice(i, 1);
                continue;
            }
            const e = easeIO(clamp(u, 0, 1));
            let x;
            let y;
            if (p.geo.path) {
                const pt = p.geo.path.getPointAtLength(p.geo.len * e);
                x = pt.x;
                y = pt.y;
            } else {
                [x, y] = bez(p.geo, e);
            }
            p.el.style.display = '';
            p.el.setAttribute('cx', x.toFixed(1));
            p.el.setAttribute('cy', y.toFixed(1));
        }
        this.paintTip(sim, reading);
        this.listN.textContent = fmt(m.active.size);
        const shown = this.slots.s.filter(z => z.id !== null && !z.doneAt).length;
        const hidden = Math.max(0, m.active.size - shown);
        this.listMore.textContent = hidden ? `+${fmt(hidden)} not shown` : '';
        this.slots.s.forEach((z, i) => this.paintSlot(this.slotEls[i], z));
        this.paintHover();
    }

    paintHover() {
        const t = this.hover != null ? this.model.titles.get(this.hover) : null;
        if (!t || (!t.path.length && !t.fate)) {
            this.hl.style.opacity = '0';
            return;
        }
        const keys = ['src', ...t.path];
        if (t.fate) keys.push(FATE_NODE[t.fate]);
        const a = this.slotAnchor(t.id);
        let d = a ? `M${a[0]},${a[1]} L${this.L.db[0] - NODE_R - 2},${this.L.db[1]}` : '';
        for (let i = 1; i < keys.length; i++) {
            const seg = this.line(keys[i - 1], keys[i]);
            d += d ? seg.replace(/^M/, ' M') : seg;
        }
        this.hl.setAttribute('d', d);
        this.hl.style.opacity = '0.9';
    }

    paintSlot(el, z) {
        if (z.id === null) {
            if (el.dataset.id !== '') {
                el.dataset.id = '';
                el.dataset.key = '';
                el.className = 'fl-slot is-empty';
            }
            return;
        }
        const t = this.model.titles.get(z.id);
        if (!t) return;
        const fate = t.fate === 'matched' && t.known ? 'known' : t.fate === 'notfound' && t.earlier ? 'cached' : t.fate;
        const blocked = !t.done && t.svc && this.model.node(t.svc).blockedUntil > this.simNow(performance.now());
        const state = t.done ? fate ?? 'done' : blocked ? 'blocked' : t.state;
        const key = `${z.id}|${state}|${t.svc || ''}`;
        if (el.dataset.key === key) return;
        el.dataset.key = key;
        el.dataset.id = String(z.id);
        el.className = 'fl-slot is-' + state;
        el.querySelector('.nm').textContent = t.name;
        el.querySelector('.yr').textContent = t.year || '';
        const st = el.querySelector('.st');
        if (t.done && fate) st.innerHTML = glyph(FATE[fate].glyph, 14);
        else if (blocked) st.innerHTML = glyph('hourglass', 13);
        else if (t.svc && (state === 'asking' || state === 'details')) st.innerHTML = tileHtml(this.brand[(state === 'details' ? 'd:' : '') + t.svc] ?? this.layout.sammlarr, 16);
        else st.innerHTML = '<span class="fl-dot"></span>';
        el.title = t.name;
    }

    dispose() {
        cancelAnimationFrame(this.raf);
        clearTimeout(this.reflowTimer);
        this.resize.disconnect();
        document.removeEventListener('visibilitychange', this.onVisibility);
        this.host.innerHTML = '';
    }
}

export function mount(host, layout, snapshot) {
    views.get(host)?.dispose();
    views.set(host, new View(host, layout, snapshot));
}

export function push(host, batch) {
    views.get(host)?.push(batch);
}

export async function fold(host) {
    host.style.overflow = 'hidden';
    await host.animate([{ height: `${host.offsetHeight}px` }, { height: '0px', opacity: 0 }], { duration: 500, easing: 'ease', fill: 'forwards' }).finished;
}

export function dispose(host) {
    views.get(host)?.dispose();
    views.delete(host);
}
