const MIN_W = 1120;
const MIN_H = 440;
const DB_X = 345;
const ID_X = 449;
const NODE_R = 22;
const NODE_GAP = 86;
const COL_GAP = 130;
const COL_MAX = 300;
const DETAIL_GAP = 120;
const DETAIL_ROWS = 4;
const TOP = 64;
const BOTTOM = 84;
const SLOT_TOP = 38;
const SLOT_H = 28;
const DELAY = 250;
const LATE = 1000;
const LINGER = 650;
const MAX_DOTS = 180;
const RING = 2 * Math.PI * NODE_R;
const SVG_NS = 'http://www.w3.org/2000/svg';

const KIND = ['started', 'sourceEnded', 'listed', 'known', 'earlier', 'path', 'asked', 'answered', 'gaveUp', 'settled'];
const OUTCOME = ['answered', 'after429', 'gaveup', 'failed'];
const FATE_KEY = ['matched', 'couldnot', 'notfound'];
const FATE_NODE = { matched: 'sink', couldnot: 'cl', notfound: 'nf' };
const FATE = {
    matched: { label: 'Matched', css: 'var(--f-matched)', glyph: 'check' },
    notfound: { label: 'Not found', css: 'var(--f-notfound)', glyph: 'x' },
    couldnot: { label: "Couldn't look up", css: 'var(--f-couldnot)', glyph: 'cloud-off' },
    known: { css: 'var(--f-known)', glyph: 'database' },
    cached: { css: 'var(--f-cached)', glyph: 'history' },
};
const STATUS_CSS = {
    answered: 'var(--usage-answered)',
    after429: 'var(--usage-after-429)',
    gaveup: 'var(--usage-gave-up)',
    failed: 'var(--usage-failed)',
    det: 'var(--usage-accent)',
};
const GLYPH = {
    check: '<path d="M20 6 9 17l-5-5"/>',
    x: '<path d="M18 6 6 18"/><path d="m6 6 12 12"/>',
    'cloud-off': '<path d="m2 2 20 20"/><path d="M5.782 5.782A7 7 0 0 0 9 19h8.5a4.5 4.5 0 0 0 1.307-.193"/><path d="M21.532 16.5A4.5 4.5 0 0 0 17.5 10h-1.79A7.008 7.008 0 0 0 10 5.07"/>',
    database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M3 5V19A9 3 0 0 0 21 19V5"/><path d="M3 12A9 3 0 0 0 21 12"/>',
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

class Model {
    constructor(keyOf) {
        this.keyOf = keyOf;
        this.titles = new Map();
        this.active = new Set();
        this.started = 0;
        this.c = { matched: 0, notfound: 0, couldnot: 0, known: 0, cached: 0 };
        this.svc = {};
        this.edges = new Map();
        this.t = 0;
        this.running = true;
    }

    node(key) {
        let s = this.svc[key];
        if (!s) {
            s = { answered: 0, after429: 0, gaveup: 0, failed: 0, inflight: 0, ema: 0, det: 0, detInflight: 0, blockedUntil: 0, busy: 0 };
            this.svc[key] = s;
        }
        return s;
    }

    edge(a, b, kind) {
        const key = `${a}>${b}|${kind === 'bypass' ? 'lane' : 'line'}`;
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
        this.c = {
            matched: f.matched - f.known,
            notfound: f.notFound - f.earlier + f.listedWithoutIds,
            couldnot: f.couldNotLookUp,
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
        const from = t.prev;
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
        return { from, to: key, kind: 'hop', key };
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
        const from = t.prev;
        t.path.push(key);
        t.prev = key;
        if (step.stage === 0) t.last = key;
        this.edge(from, key, 'hop');
        return { flash: key, status: 'gaveup', from, to: key, kind: 'hop', key };
    }

    settled(t, fate) {
        t.fate = fate;
        if (fate === 'matched' && t.known && !t.path.length) {
            this.c.known++;
            this.edge('src', 'sink', 'bypass');
            this.finish(t);
            return { from: 'src', to: 'sink', kind: 'bypass', fate: 'known', finished: true };
        }
        if (fate === 'notfound' && t.earlier) {
            this.c.cached++;
            this.edge('src', 'nf', 'bypass');
            this.finish(t);
            return { from: 'src', to: 'nf', kind: 'bypass', fate: 'cached', finished: true };
        }
        if (t.known) this.c.known++;
        else this.c[fate]++;
        const from = fate === 'matched' ? (t.lastDet ?? t.last ?? 'src') : (t.last ?? 'src');
        const to = FATE_NODE[fate];
        this.edge(from, to, 'fate-' + fate);
        this.finish(t);
        return { from, to, kind: 'fate', fate, finished: true };
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
    constructor(host, layout, snapshot) {
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
        this.model = new Model((service, stage) => this.keys[`${service}:${stage}`] ?? `${stage === 1 ? 'd:' : ''}svc${service}`);
        this.model.seed(snapshot);
        this.q = [];
        this.qi = 0;
        this.anchor = performance.now() - snapshot.elapsedMs;
        this.lastSeq = snapshot.cursorSeq;
        this.lastDeliver = performance.now();
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
        const tail = detailCols ? 161 + (detailCols - 1) * DETAIL_GAP + 148 : 309;
        const W = Math.max(MIN_W, ID_X + (columns - 1) * COL_GAP + tail + 80, Math.floor(width || 0));
        const outX = W - 80;
        const detailX = c => outX - 148 - (detailCols - 1 - c) * DETAIL_GAP;
        const idEnd = detailCols ? detailX(0) - 161 : outX - 309;
        const step = columns > 1 ? Math.min((idEnd - ID_X) / (columns - 1), COL_MAX) : 0;
        const first = columns > 1 ? ID_X + (idEnd - ID_X - step * (columns - 1)) / 2 : (ID_X + idEnd) / 2;
        const mid = H / 2;
        const L = { db: [DB_X, Math.round(mid - 15)], sink: [outX, Math.round(mid - 108)], cl: [outX, Math.round(mid + 2)], nf: [outX, Math.round(mid + 112)], pos: {} };
        byColumn.forEach((inColumn, c) => {
            inColumn.forEach((n, i) => {
                L.pos[n.key] = [Math.round(first + c * step), Math.round(spread(inColumn.length, TOP, H - BOTTOM, i))];
            });
        });
        details.forEach((n, i) => {
            const c = Math.floor(i / detailRows);
            const inColumn = Math.min(detailRows, details.length - c * detailRows);
            const span = Math.min(H - BOTTOM - TOP, (inColumn - 1) * NODE_GAP * 1.25);
            L.pos['d:' + n.key] = [Math.round(detailX(c)), Math.round(spread(inColumn, mid - span / 2, mid + span / 2, i % detailRows))];
        });
        this.W = W;
        this.H = H;
        this.idX = Math.round(first);
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
        return k === 'sink' || k === 'nf' || k === 'cl' ? 17 : NODE_R + 2;
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

    lane(b) {
        const [x, y] = this.L.db;
        const [tx, ty] = this.pos(b);
        const top = b === 'sink';
        const edge = top ? 8 : this.H - 8;
        const turn = top ? 28 : this.H - 28;
        const sy = top ? y - NODE_R - 2 : y + NODE_R + 2;
        return `M${x},${sy} L${x},${turn} Q${x},${edge} ${x + 20},${edge} L${tx - 72},${edge} C${tx - 32},${edge} ${tx - 32},${ty} ${tx - 17},${ty}`;
    }

    pathD(p) {
        return `M${p[0][0]},${p[0][1]} C${p[1][0]},${p[1][1]} ${p[2][0]},${p[2][1]} ${p[3][0]},${p[3][1]}`;
    }

    build() {
        const L = this.L;
        const detailsX = this.detailX;
        this.host.innerHTML = `<div class="fl-fit"><div class="fl-view">
            <div class="fl-list"><div class="fl-lh">In flight <b class="fl-n">0</b><span class="fl-more"></span></div>
            ${Array.from({ length: this.slotN }, (_, i) => `<div class="fl-slot is-empty" data-i="${i}" style="top:${SLOT_TOP + i * SLOT_H}px"><span class="st"></span><span class="nm"></span><span class="yr"></span></div>`).join('')}</div>
            <svg class="fl-svg" viewBox="0 0 ${this.W} ${this.H}" width="${this.W}" height="${this.H}">
                <text class="fl-cap" x="${this.idX - 40}" y="26">Services</text>${detailsX ? `<text class="fl-cap" x="${detailsX - 28}" y="26">Details</text>` : ''}<text class="fl-cap" x="${L.sink[0] - 32}" y="26">Outcome</text>
                <g class="g-static"></g><g class="g-edges"></g><path class="fl-hl"/><g class="g-parts"></g><g class="g-nodes"></g>
            </svg></div></div>`;
        this.inner = this.host.querySelector('.fl-fit');
        this.inner.style.width = `${this.W}px`;
        this.host.querySelector('.fl-view').style.height = `${this.H}px`;
        this.fit();
        const svg = this.host.querySelector('svg');
        this.gEdges = svg.querySelector('.g-edges');
        this.gParts = svg.querySelector('.g-parts');
        this.hl = svg.querySelector('.fl-hl');
        const out = [];
        for (const n of this.layout.nodes) out.push(this.nodeHtml(n.key, n.brand, this.pos(n.key), n.brand.name));
        for (const n of this.layout.details) out.push(this.nodeHtml('d:' + n.key, n.brand, this.pos('d:' + n.key), n.brand.name + ' details'));
        out.push(this.dbHtml());
        for (const [k, f] of [['sink', 'matched'], ['cl', 'couldnot'], ['nf', 'notfound']]) {
            out.push(`<g class="fl-fate" transform="translate(${L[k][0]},${L[k][1]})" style="stroke:${FATE[f].css};fill:${FATE[f].css}"><circle r="16" style="fill:color-mix(in oklch, ${FATE[f].css} 18%, var(--card))" stroke-width="2"/>${MARKS[f]}<text class="fl-fate-name" y="34">${FATE[f].label}</text><text class="fl-fate-cap fc-${k}" y="48"></text></g>`);
        }
        svg.querySelector('.g-nodes').innerHTML = out.join('');
        this.nd = {};
        for (const g of svg.querySelectorAll('.fl-nd')) {
            this.nd[g.dataset.k] = {
                g,
                segs: [...g.querySelectorAll('.nd-seg')],
                stat: g.querySelector('.nd-stat'),
                halo: g.querySelector('.nd-halo'),
                badge: g.querySelector('.nd-badge'),
                badgeT: g.querySelector('.nd-badge text'),
                flag: g.querySelector('.nd-flag'),
                flash: 0,
                flashColor: '',
            };
        }
        this.dbSegs = [...svg.querySelectorAll('.db-seg')];
        this.dbStat = svg.querySelector('.db-stat');
        this.dbNew = svg.querySelector('.db-new');
        this.fc = { sink: svg.querySelector('.fc-sink'), nf: svg.querySelector('.fc-nf'), cl: svg.querySelector('.fc-cl') };
        const gs = svg.querySelector('.g-static');
        gs.innerHTML =
            this.layout.edges.map(e => `<path class="fl-static" d="${this.pathD(this.geo(e.from ?? 'src', e.to))}"/>`).join('') +
            `<path class="fl-static lane-sink" d="${this.lane('sink')}"/><path class="fl-static lane-nf" d="${this.lane('nf')}"/>`;
        this.lanes = {};
        for (const k of ['sink', 'nf']) {
            const el = gs.querySelector('.lane-' + k);
            this.lanes[k] = { path: el, len: el.getTotalLength() };
        }
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
            <circle class="nd-halo" r="${NODE_R + 9}" style="fill:${brand.color}"/><circle class="nd-bg" r="${NODE_R}"/><circle class="nd-track" r="${NODE_R}"/>
            ${['answered', 'after429', 'gaveup', 'failed', 'det'].map(c => `<circle class="nd-seg" r="${NODE_R}" style="stroke:${STATUS_CSS[c]}" stroke-dasharray="0 ${RING}" transform="rotate(-90)"/>`).join('')}
            ${svgTile(brand, 26)}
            <text class="nd-name" y="${NODE_R + 19}">${escape(label)}</text><text class="nd-stat" y="${NODE_R + 33}"></text>
            <text class="nd-flag" y="${-NODE_R - 10}"></text>
            <g class="nd-badge" transform="translate(${NODE_R - 2},${-NODE_R + 4})" style="display:none"><rect x="-13" y="-9" width="26" height="17" rx="8.5"/><text y="4"></text></g></g>`;
    }

    dbHtml() {
        const [x, y] = this.L.db;
        return `<g transform="translate(${x},${y})"><circle class="nd-bg" r="${NODE_R}"/><circle class="nd-track" r="${NODE_R}"/>
            ${['var(--f-known)', 'var(--f-cached)', 'var(--sammlarr-gold)'].map(c => `<circle class="db-seg" r="${NODE_R}" fill="none" stroke-width="5" style="stroke:${c}" stroke-dasharray="0 ${RING}" transform="rotate(-90)"/>`).join('')}
            ${svgTile(this.layout.sammlarr, 26)}<text class="nd-name" y="${NODE_R + 19}">Sammlarr</text><text class="nd-stat db-stat" y="${NODE_R + 33}"></text><text class="nd-stat db-new" y="${NODE_R + 47}"></text></g>`;
    }

    edgeEl(e) {
        let x = this.E.get(e.key);
        if (x) return x;
        const el = document.createElementNS(SVG_NS, 'path');
        el.setAttribute('class', 'fl-ed ' + (e.kind === 'bypass' ? 'is-bypass' : e.kind.startsWith('fate') ? 'is-' + e.kind : ''));
        el.setAttribute('d', e.kind === 'bypass' ? this.lane(e.b) : this.pathD(this.geo(e.a, e.b)));
        this.gEdges.appendChild(el);
        x = { el, e };
        this.E.set(e.key, x);
        return x;
    }

    slotAnchor(id) {
        const i = this.slots.of.get(id);
        return i == null ? null : [262, SLOT_TOP + i * SLOT_H + 12];
    }

    push(batch) {
        const now = performance.now();
        for (const c of batch.capacity) {
            const key = this.keys[`${c.service}:0`];
            if (!key) continue;
            const s = this.model.node(key);
            s.blockedUntil = c.blockedUntilMs;
            s.busy = c.busy;
        }
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
        let n = 0;
        while (this.qi < this.q.length && this.q[this.qi][0] <= now) {
            const step = this.q[this.qi++][1];
            this.onStep(step, now, this.model.apply(step));
            n++;
        }
        if (this.qi > 2000) {
            this.q = this.q.slice(this.qi);
            this.qi = 0;
        }
        if (n) this.lastDeliver = now;
        if (this.endAt && now >= this.endAt && this.model.running) this.model.end();
        this.render(now);
    }

    simNow(now) {
        return this.model.t + (this.model.running ? clamp(now - this.lastDeliver, 0, 400) : 0);
    }

    onStep(step, now, fx) {
        if (fx?.started) {
            this.slots.take(step.title, now);
            const a = this.slotAnchor(step.title);
            if (a) {
                this.spawn(this.geo('src', 'src', a), 'var(--muted-foreground)', now, 240);
                this.entryEnd.set(step.title, now + 240);
            }
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
        if (fx.to) {
            const t0 = fx.from === 'src' ? Math.max(now, this.entryEnd.get(step.title) || now) : now;
            const color = fx.kind === 'hop' ? (this.brand[fx.key]?.color ?? 'var(--muted-foreground)') : FATE[fx.fate].css;
            this.spawn(fx.kind === 'bypass' ? this.lanes[fx.to] : this.geo(fx.from, fx.to), color, t0, fx.kind === 'bypass' ? 620 : 300);
        }
        if (fx.finished) {
            this.slots.release(step.title, now);
            this.entryEnd.delete(step.title);
        }
    }

    spawn(geo, color, t0, dur) {
        if (this.parts.length > MAX_DOTS) return;
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
        const keys = ['answered', 'after429', 'gaveup', 'failed', 'det'];
        const sim = this.simNow(now);
        for (const k of Object.keys(this.nd)) {
            const n = this.nd[k];
            const det = k.startsWith('d:');
            const v = m.node(k);
            const parts = det ? { det: v.det } : { answered: v.answered, after429: v.after429, gaveup: v.gaveup, failed: v.failed };
            const total = Object.values(parts).reduce((a, b) => a + b, 0);
            let off = 0;
            n.segs.forEach((seg, i) => {
                const len = total ? (RING * (parts[keys[i]] || 0)) / total : 0;
                seg.setAttribute('stroke-dasharray', `${len} ${RING}`);
                seg.setAttribute('stroke-dashoffset', `${-off}`);
                off += len;
            });
            const done = det ? v.det : v.answered + v.after429 + v.gaveup + v.failed;
            const asked = done + (det ? v.detInflight : v.inflight);
            const blocked = !det && m.running && v.blockedUntil > sim;
            n.g.classList.toggle('is-idle', asked === 0);
            n.g.classList.toggle('is-blocked', blocked);
            const inMemory = !det && this.layout.nodes.find(x => x.key === k)?.inMemory;
            n.stat.textContent = asked === 0 ? 'not asked' : inMemory ? `${fmt(asked)} lookups · in memory` : `${fmt(asked)} asked · ${Math.round(v.ema)} ms`;
            n.flag.textContent = blocked ? `429 · free in ${Math.max(0, Math.ceil((v.blockedUntil - sim) / 1000))} s` : '';
            const w = det ? v.detInflight : v.inflight;
            n.badge.style.display = w ? '' : 'none';
            if (w) {
                n.badgeT.textContent = fmt(w);
                const wd = String(fmt(w)).length * 6.5 + 12;
                n.badge.firstElementChild.setAttribute('width', wd);
                n.badge.firstElementChild.setAttribute('x', -wd / 2);
            }
            const fl = n.flash ? Math.max(0, 1 - (now - n.flash) / 380) : 0;
            n.halo.style.opacity = String(Math.min(0.42, (det ? v.detInflight / 14 : v.busy) * 0.3 + fl * 0.35));
            n.halo.style.fill = fl > 0 ? n.flashColor : this.brand[k].color;
        }
        const vals = [m.c.known, m.c.cached, Math.max(0, m.started - m.c.known - m.c.cached)];
        const dbTotal = vals.reduce((a, b) => a + b, 0);
        let off = 0;
        this.dbSegs.forEach((seg, i) => {
            const len = dbTotal ? (RING * vals[i]) / dbTotal : 0;
            seg.setAttribute('stroke-dasharray', `${len} ${RING}`);
            seg.setAttribute('stroke-dashoffset', `${-off}`);
            off += len;
        });
        this.dbStat.textContent = `${fmt(m.c.known)} known · ${fmt(m.c.cached)} earlier`;
        this.dbNew.textContent = `${fmt(vals[2])} new`;
        this.fc.sink.textContent = fmt(m.c.matched) + (m.c.known ? ` + ${fmt(m.c.known)} known` : '');
        this.fc.nf.textContent = fmt(m.c.notfound) + (m.c.cached ? ` + ${fmt(m.c.cached)} earlier` : '');
        this.fc.cl.textContent = fmt(m.c.couldnot);
        for (const e of m.edges.values()) {
            const x = this.edgeEl(e);
            const recent = Math.max(0, 1 - (m.t - e.last) / 900);
            x.el.setAttribute('stroke-width', (1 + 2.4 * Math.log10(1 + e.n)).toFixed(2));
            x.el.style.opacity = (m.running ? 0.22 + 0.6 * recent : 0.5).toFixed(2);
        }
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
        this.listN.textContent = fmt(m.active.size);
        const shown = this.slots.s.filter(z => z.id !== null && !z.doneAt).length;
        const hidden = Math.max(0, m.active.size - shown);
        this.listMore.textContent = hidden ? `+${fmt(hidden)} not shown` : '';
        this.slots.s.forEach((z, i) => this.paintSlot(this.slotEls[i], z));
        this.paintHover();
    }

    paintHover() {
        const t = this.hover != null ? this.model.titles.get(this.hover) : null;
        if (!t || !t.path.length) {
            this.hl.style.opacity = '0';
            return;
        }
        const keys = ['src', ...t.path];
        if (t.fate) keys.push(FATE_NODE[t.fate]);
        const a = this.slotAnchor(t.id);
        let d = a ? `M${a[0]},${a[1]} L${this.L.db[0] - NODE_R - 2},${this.L.db[1]}` : '';
        for (let i = 1; i < keys.length; i++) {
            const seg = this.pathD(this.geo(keys[i - 1], keys[i]));
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

export function fold(host) {
    host.style.height = `${host.offsetHeight}px`;
    requestAnimationFrame(() => host.classList.add('is-folded'));
    return new Promise(resolve => setTimeout(resolve, 550));
}

export function dispose(host) {
    views.get(host)?.dispose();
    views.delete(host);
}
