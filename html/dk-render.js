// DisplayKit scene JSON -> DOM, emulating UI Toolkit (Yoga flexbox) defaults with CSS.
// Used by index.html (live viewer) and render.html (headless PNG/layout capture).
// UITK-vs-CSS differences handled here are listed in ../README.md ("HTML mapping").
(function (global) {
  'use strict';

  // FontType -> [file, hhea ascender, hhea descender] in em. TextCore sizes a line by ascender-descender.
  const MONO = [1.048, 0.271], ROBOTO = [0.928, 0.244];
  const FONTS = {
    Default: ['Inter-Regular', 0.969, 0.241],
    LiberationSans: ['LiberationSans-Regular', 0.905, 0.245],
  };
  for (const w of ['Regular', 'Italic', 'Bold', 'BoldItalic', 'Light', 'LightItalic', 'Medium', 'MediumItalic', 'Thin', 'ThinItalic']) {
    FONTS['Roboto' + w] = ['Roboto-' + w, ...ROBOTO];
    FONTS['RobotoMono' + w] = ['RobotoMono-' + w, ...MONO];
  }

  // The 15.0 client's text metrics where they differ from the browser (fitted against 1920x1080 client captures; see
  // README "15.0 client text metrics"). opts.metrics === 'reference' turns them off and draws plain UI Toolkit/CSS.
  const CLIENT = {
    // The CJK fallback (Microsoft YaHei, hhea ascender/descender). TextCore sets a line's baseline by the tallest
    // ascender on it, so a line with CJK in a font with a shorter ascender (Roboto) sits lower and grows.
    cjkAscent: 1.058, cjkDescent: 0.262,
    // Its CJK glyphs also land about 1px lower than the browser draws them, at every measured size (13-60px).
    cjkDrop: 1,
    // An auto-sized Roboto / Roboto Mono label is this much taller than its line; the alignment places the line in it.
    lineExtra: { Roboto: 2 },
  };
  const family = type => (/^Roboto/.test(type) ? 'Roboto' : type);

  // Calibrated against the wiki's in-game screenshots (see README): the client's default text.
  const DEFAULTS = { font: 'Default', fontSize: 14, color: [1, 1, 1, 1] };

  const CJK = /[⺀-⿿　-〿぀-ヿ㄀-ㇿ㐀-䶿一-鿿가-힯豈-﫿︰-﹏＀-￯]/;
  const CJK_G = new RegExp(CJK.source, 'g');
  const CJK_RUN = new RegExp(CJK.source + '+', 'g');
  const CJK_FALLBACK = '"Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC", sans-serif';

  let fontsInstalled = false;
  function installFonts(fontBase) {
    if (fontsInstalled) return;
    fontsInstalled = true;
    const css = Object.entries(FONTS).map(([type, [file]]) =>
      `@font-face{font-family:"dk-${type}";src:url("${fontBase}${file}.ttf") format("truetype");font-display:block;}`).join('\n');
    const style = document.createElement('style');
    style.textContent = css;
    document.head.appendChild(style);
  }

  function fontsReady() {
    const loads = Object.keys(FONTS).map(t => document.fonts.load(`16px "dk-${t}"`).catch(() => null));
    return Promise.all(loads).then(() => document.fonts.ready);
  }

  const ENUM = {
    flexDirection: { Column: 'column', ColumnReverse: 'column-reverse', Row: 'row', RowReverse: 'row-reverse' },
    flexWrap: { NoWrap: 'nowrap', Wrap: 'wrap', WrapReverse: 'wrap-reverse' },
    alignItems: 'align', alignSelf: 'align', alignContent: 'align',
    justifyContent: { FlexStart: 'flex-start', Center: 'center', FlexEnd: 'flex-end', SpaceBetween: 'space-between', SpaceAround: 'space-around', SpaceEvenly: 'space-evenly' },
    position: { Relative: 'relative', Absolute: 'absolute' },
    display: { Flex: 'flex', None: 'none' },
    visibility: { Visible: 'visible', Hidden: 'hidden' },
    overflow: { Visible: 'visible', Hidden: 'hidden', Scroll: 'hidden' },
  };
  const ALIGN = { Auto: 'auto', FlexStart: 'flex-start', Center: 'center', FlexEnd: 'flex-end', Stretch: 'stretch', SpaceBetween: 'space-between', SpaceAround: 'space-around' };
  const CSS_NAME = {
    flexGrow: 'flex-grow', flexShrink: 'flex-shrink', flexBasis: 'flex-basis', flexDirection: 'flex-direction', flexWrap: 'flex-wrap',
    alignItems: 'align-items', alignSelf: 'align-self', alignContent: 'align-content', justifyContent: 'justify-content',
    width: 'width', height: 'height', minWidth: 'min-width', minHeight: 'min-height', maxWidth: 'max-width', maxHeight: 'max-height',
    marginTop: 'margin-top', marginBottom: 'margin-bottom', marginLeft: 'margin-left', marginRight: 'margin-right',
    paddingTop: 'padding-top', paddingBottom: 'padding-bottom', paddingLeft: 'padding-left', paddingRight: 'padding-right',
    borderTopWidth: 'border-top-width', borderBottomWidth: 'border-bottom-width', borderLeftWidth: 'border-left-width', borderRightWidth: 'border-right-width',
    borderTopColor: 'border-top-color', borderBottomColor: 'border-bottom-color', borderLeftColor: 'border-left-color', borderRightColor: 'border-right-color',
    borderTopLeftRadius: 'border-top-left-radius', borderTopRightRadius: 'border-top-right-radius',
    borderBottomLeftRadius: 'border-bottom-left-radius', borderBottomRightRadius: 'border-bottom-right-radius',
    position: 'position', top: 'top', bottom: 'bottom', left: 'left', right: 'right',
    display: 'display', visibility: 'visibility', opacity: 'opacity', overflow: 'overflow', backgroundColor: 'background-color',
  };
  // Text properties UI Toolkit inherits down the tree (text-overflow is the exception and stays local).
  const INHERITED = ['font', 'fontSize', 'color', 'unityFontStyleAndWeight', 'unityTextAlign', 'whiteSpace', 'letterSpacing',
    'wordSpacing', 'unityParagraphSpacing', 'unityTextOutlineWidth', 'unityTextOutlineColor', 'textShadow'];

  const rgba = c => `rgba(${Math.round(c[0] * 255)},${Math.round(c[1] * 255)},${Math.round(c[2] * 255)},${c[3]})`;
  const px = v => (typeof v === 'number' ? v + 'px' : v);
  const lengthPx = v => (typeof v === 'string' && v.endsWith('px') ? parseFloat(v) : typeof v === 'number' ? v : null);

  function cssValue(key, v) {
    if (v === 'initial') return null;
    if (key.endsWith('Color')) return Array.isArray(v) ? rgba(v) : null;
    const map = ENUM[key];
    if (map === 'align') return ALIGN[v] || null;
    if (map) return map[v] || null;
    if (key === 'flexGrow' || key === 'flexShrink' || key === 'opacity') return String(v);
    if (key.startsWith('border') && key.endsWith('Width')) return px(v);
    return px(v);
  }

  // UITK rich text (TextCore tags) -> HTML. Unknown tags render literally, like TextCore.
  function richText(text, tofu, cjkDrop) {
    const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const drop = s => (cjkDrop ? s.replace(CJK_RUN, m => `<span style="position:relative;top:${cjkDrop}px">${m}</span>`) : s);
    // UI Toolkit breaks lines at "\n" in every white-space mode; CSS 'normal' would fold it into a space.
    const plain = s => drop(esc(tofu ? s.replace(CJK_G, '□') : s)).replace(/\r?\n/g, '<br>');
    let out = '', i = 0;
    const open = [];
    const re = /<(\/?)([a-z]+)(?:=("?)([^>"]*)\3)?>/gi;
    let m;
    while ((m = re.exec(text))) {
      const [all, close, tagRaw, , arg] = m;
      const tag = tagRaw.toLowerCase();
      let html = null;
      if (!close) {
        switch (tag) {
          case 'b': html = '<b>'; break;
          case 'i': html = '<i>'; break;
          case 'u': html = '<u>'; break;
          case 's': html = '<s>'; break;
          case 'sup': html = '<sup>'; break;
          case 'sub': html = '<sub>'; break;
          case 'br': html = '<br>'; break;
          case 'color': if (arg) html = `<span style="color:${esc(arg)}">`; break;
          case 'size': if (arg) html = `<span style="font-size:${/%$/.test(arg) ? arg : /em$/.test(arg) ? arg : parseFloat(arg) + 'px'}">`; break;
          case 'alpha': if (arg) html = `<span style="opacity:${parseInt(arg.replace('#', ''), 16) / 255}">`; break;
          case 'mark': if (arg) html = `<span style="background:${esc(arg)}">`; break;
          case 'cspace': if (arg) html = `<span style="letter-spacing:${parseFloat(arg)}px">`; break;
        }
        if (html && tag !== 'br') open.push(tag);
      } else {
        const tagMap = { b: '</b>', i: '</i>', u: '</u>', s: '</s>', sup: '</sup>', sub: '</sub>' };
        if (tagMap[tag]) html = tagMap[tag];
        else if (['color', 'size', 'alpha', 'mark', 'cspace'].includes(tag)) html = '</span>';
      }
      if (html === null) continue;
      out += plain(text.slice(i, m.index)) + html;
      i = m.index + all.length;
    }
    return out + plain(text.slice(i));
  }

  function anchorParts(a) {
    const v = /^Upper/.test(a) ? 'flex-start' : /^Lower/.test(a) ? 'flex-end' : 'center';
    const h = /Left$/.test(a) ? 'left' : /Right$/.test(a) ? 'right' : 'center';
    return [v, h];
  }

  function buildElement(node, path, inherited, opts, sink) {
    const el = document.createElement('div');
    el.className = 'dk-el dk-' + node.type;
    el.dataset.path = path;
    const style = node.style || {};
    const text = Object.assign({}, inherited);
    for (const k of INHERITED) if (k in style && style[k] !== 'initial') text[k] = style[k];

    for (const [k, v] of Object.entries(style)) {
      const name = CSS_NAME[k];
      if (!name) continue;
      const css = cssValue(k, v);
      if (css !== null) el.style.setProperty(name, css);
    }
    // Border colour without an explicit colour: UITK's initial border colour (guess: black, like USS docs).
    const t = style.translate, sc = style.scale, r = style.rotate, o = style.transformOrigin;
    if (t && typeof t === 'object') el.style.translate = `${t.x} ${t.y}`;
    if (Array.isArray(sc)) el.style.scale = `${sc[0]} ${sc[1]}`;
    if (r && typeof r === 'object') el.style.rotate = `${r.angle}deg`;
    if (o && typeof o === 'object') el.style.transformOrigin = `${o.x} ${o.y}`;

    if (node.type === 'text') {
      const fontType = text.font || DEFAULTS.font;
      const [, asc, desc] = FONTS[fontType] || FONTS.Default;
      const fontSize = lengthPx(text.fontSize) ?? DEFAULTS.fontSize;
      const [vAlign, hAlign] = anchorParts(text.unityTextAlign || 'UpperLeft');
      const content = node.content || '';
      const hasCjk = CJK.test(content.replace(/<[^>]*>/g, ''));

      el.style.justifyContent = vAlign;
      const label = document.createElement('span');
      label.className = 'dk-label';
      const fs = text.unityFontStyleAndWeight || 'Normal';
      label.style.fontFamily = `"dk-${fontType}", ${opts.cjk === 'tofu' ? 'sans-serif' : CJK_FALLBACK}`;
      label.style.fontSize = fontSize + 'px';
      label.style.lineHeight = asc + desc;
      if (opts.metrics !== 'reference') clientMetrics(label, fontType, fontSize, asc, desc, hasCjk, vAlign);
      label.style.fontWeight = /Bold/.test(fs) ? '700' : '400';
      label.style.fontStyle = /Italic/.test(fs) ? 'italic' : 'normal';
      label.style.color = rgba(Array.isArray(text.color) ? text.color : DEFAULTS.color);
      label.style.textAlign = hAlign;
      label.style.whiteSpace = { NoWrap: 'nowrap', Pre: 'pre', PreWrap: 'pre-wrap' }[text.whiteSpace] || 'normal';
      if (text.letterSpacing) label.style.letterSpacing = px(text.letterSpacing);
      if (text.wordSpacing) label.style.wordSpacing = px(text.wordSpacing);
      if (style.textOverflow === 'Ellipsis') { label.style.textOverflow = 'ellipsis'; label.style.overflow = 'hidden'; }
      const shadows = [];
      const sh = text.textShadow;
      if (sh && typeof sh === 'object') shadows.push(`${sh.offset[0]}px ${sh.offset[1]}px ${sh.blur}px ${rgba(sh.color)}`);
      if (shadows.length) label.style.textShadow = shadows.join(',');
      const ow = text.unityTextOutlineWidth;
      if (typeof ow === 'number' && ow > 0) {
        label.style.webkitTextStroke = `${ow * 2}px ${rgba(Array.isArray(text.unityTextOutlineColor) ? text.unityTextOutlineColor : [0, 0, 0, 1])}`;
        label.style.paintOrder = 'stroke fill';
      }
      let html = richText(content, opts.cjk === 'tofu', opts.metrics !== 'reference' ? CLIENT.cjkDrop : 0);
      const para = lengthPx(text.unityParagraphSpacing);
      if (para) html = html.split('\n').map(p => `<span style="display:block;margin-bottom:${para}px">${p}</span>`).join('');
      label.innerHTML = html;
      el.appendChild(label);
      sink.texts.push({ el, path, fontType, fontSize, hasCjk, style: fs, content });
    }

    for (const child of node.children || [])
      el.appendChild(buildElement(child, path + '/' + child.id, text, opts, sink));
    return el;
  }

  /** Pads the label so its line sits where the 15.0 client draws it (single-line labels; see CLIENT). */
  function clientMetrics(label, fontType, fontSize, asc, desc, hasCjk, vAlign) {
    let top = hasCjk ? Math.max(0, CLIENT.cjkAscent - asc) * fontSize : 0;
    let bottom = hasCjk ? Math.max(0, CLIENT.cjkDescent - desc) * fontSize : 0;
    const extra = CLIENT.lineExtra[family(fontType)] || 0;
    const share = vAlign === 'flex-start' ? 0 : vAlign === 'center' ? 0.5 : 1;
    top += extra * share;
    bottom += extra * (1 - share);
    if (top) label.style.paddingTop = top + 'px';
    if (bottom) label.style.paddingBottom = bottom + 'px';
  }

  function panelScale(width, height, scale) {
    if (!scale || scale === 'constant') return 1;
    const [rw, rh] = scale.split('x').map(Number);
    // Unity PanelScaleMode.ScaleWithScreenSize, MatchWidthOrHeight with match 0.5 (log-space average).
    return Math.pow(2, (Math.log2(width / rw) + Math.log2(height / rh)) / 2);
  }

  function visibleCanvases(frame, viewer) {
    return frame.canvases
      .filter(c => c.observers.some(o => o.id === viewer && o.visible))
      .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.id - b.id);
  }

  /**
   * Render one frame into `stage` (cleared). opts: { frame, viewer, width, height, scale, cjk:'fallback'|'tofu' }.
   * Returns { scale, canvases, texts } describing what was drawn.
   */
  function render(stage, scene, opts) {
    const frames = scene.frames;
    const frame = frames[Math.max(0, Math.min(frames.length - 1, opts.frame ?? frames.length - 1))];
    const viewer = opts.viewer ?? frame.players[0];
    const s = panelScale(opts.width, opts.height, opts.scale);
    stage.innerHTML = '';
    stage.style.width = opts.width + 'px';
    stage.style.height = opts.height + 'px';
    const panel = document.createElement('div');
    panel.className = 'dk-panel';
    panel.style.width = opts.width / s + 'px';
    panel.style.height = opts.height / s + 'px';
    panel.style.transform = s === 1 ? '' : `scale(${s})`;
    stage.appendChild(panel);

    const sink = { texts: [] };
    const shown = visibleCanvases(frame, viewer);
    for (const canvas of shown) {
      const layer = document.createElement('div');
      layer.className = 'dk-layer';
      layer.dataset.canvas = canvas.id;
      layer.appendChild(buildElement(canvas.root, 'c' + canvas.id, {}, opts, sink));
      panel.appendChild(layer);
    }
    return { scale: s, frame, viewer, canvases: shown, texts: sink.texts, panel };
  }

  /** Element rects in panel units, keyed by the same path the Unity renderer writes. */
  function layout(stage, scale) {
    const base = stage.getBoundingClientRect();
    return [...stage.querySelectorAll('.dk-el')].map(el => {
      const r = el.getBoundingClientRect();
      const q = v => Math.round(v * 100) / 100;
      return { path: el.dataset.path, x: q((r.left - base.left) / scale), y: q((r.top - base.top) / scale), w: q(r.width / scale), h: q(r.height / scale) };
    });
  }

  global.DkRender = { FONTS, CLIENT, DEFAULTS, installFonts, fontsReady, render, layout, visibleCanvases, CJK };
})(window);
