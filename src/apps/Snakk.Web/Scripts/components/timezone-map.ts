/**
 * Timezone Map — interactive world map (mapsvg.com CC0 SVG) for timezone selection.
 * Loaded only on the Settings page via <object> tag.
 * - Single-timezone country: click selects the timezone directly.
 * - Multi-timezone country (US, CA, RU, AU, BR): click filters the dropdown to
 *   that country's timezones; user picks from those.
 */

(function () {
    'use strict';

    const obj = document.getElementById('timezone-map-object') as HTMLObjectElement | null;
    if (!obj) return;

    const select = document.getElementById('timezone-select') as HTMLSelectElement | null;
    if (!select) return;

    const tooltipEl = document.getElementById('tz-map-tooltip');
    const showAllBtn = document.getElementById('tz-show-all');

    // --- Country → IANA timezone (single-tz countries) ---
    // Each country maps to its own canonical timezone that exists in the dropdown.

    const countryToTz: Record<string, string> = {
        // Americas — Central & Caribbean
        GT: 'America/Guatemala', HN: 'America/Guatemala', SV: 'America/Guatemala',
        NI: 'America/Guatemala', CR: 'America/Guatemala', BZ: 'America/Guatemala',
        CU: 'America/Havana', JM: 'America/Jamaica', PA: 'America/Panama',
        CO: 'America/Bogota', EC: 'America/Guayaquil', PE: 'America/Lima',
        VE: 'America/Caracas',
        DO: 'America/Santo_Domingo', HT: 'America/Santo_Domingo',
        PR: 'America/Santo_Domingo', VI: 'America/Santo_Domingo',
        BB: 'America/Santo_Domingo', AG: 'America/Santo_Domingo',
        DM: 'America/Santo_Domingo', GD: 'America/Santo_Domingo',
        KN: 'America/Santo_Domingo', LC: 'America/Santo_Domingo',
        VC: 'America/Santo_Domingo', TT: 'America/Santo_Domingo',
        BS: 'America/Santo_Domingo', KY: 'America/Santo_Domingo',
        TC: 'America/Santo_Domingo', BM: 'America/Santo_Domingo',
        AI: 'America/Santo_Domingo', AW: 'America/Santo_Domingo',
        BL: 'America/Santo_Domingo', BQ: 'America/Santo_Domingo',
        CW: 'America/Santo_Domingo', GP: 'America/Santo_Domingo',
        MF: 'America/Santo_Domingo', MQ: 'America/Santo_Domingo',
        MS: 'America/Santo_Domingo', SX: 'America/Santo_Domingo',
        VG: 'America/Santo_Domingo',
        // Americas — South
        AR: 'America/Argentina/Buenos_Aires',
        UY: 'America/Montevideo',
        PY: 'America/Asuncion',
        BO: 'America/La_Paz',
        GY: 'America/La_Paz',
        SR: 'America/Paramaribo',
        GF: 'America/Cayenne',
        FK: 'Atlantic/Stanley',
        GL: 'America/Godthab',
        PM: 'America/Halifax',

        // Europe
        GB: 'Europe/London', IE: 'Europe/Dublin', PT: 'Europe/Lisbon',
        GG: 'Europe/London', JE: 'Europe/London', IM: 'Europe/London',
        GI: 'Europe/Madrid', FO: 'Europe/London',
        IS: 'Atlantic/Reykjavik', AX: 'Europe/Helsinki',
        NO: 'Europe/Oslo', SJ: 'Europe/Oslo', SE: 'Europe/Stockholm', DK: 'Europe/Copenhagen',
        FR: 'Europe/Paris', MC: 'Europe/Paris',
        DE: 'Europe/Berlin', LI: 'Europe/Berlin',
        NL: 'Europe/Amsterdam',
        BE: 'Europe/Brussels', LU: 'Europe/Brussels',
        ES: 'Europe/Madrid', AD: 'Europe/Madrid',
        IT: 'Europe/Rome', SM: 'Europe/Rome', VA: 'Europe/Rome',
        CH: 'Europe/Zurich',
        AT: 'Europe/Vienna',
        CZ: 'Europe/Prague',
        PL: 'Europe/Warsaw',
        HU: 'Europe/Budapest',
        SK: 'Europe/Bratislava',
        SI: 'Europe/Ljubljana',
        HR: 'Europe/Zagreb',
        RS: 'Europe/Belgrade', ME: 'Europe/Belgrade', BA: 'Europe/Belgrade',
        MK: 'Europe/Belgrade', XK: 'Europe/Belgrade',
        AL: 'Europe/Tirane',
        MT: 'Europe/Malta',
        FI: 'Europe/Helsinki',
        EE: 'Europe/Tallinn',
        LV: 'Europe/Riga',
        LT: 'Europe/Vilnius',
        GR: 'Europe/Athens', CY: 'Asia/Nicosia',
        RO: 'Europe/Bucharest',
        BG: 'Europe/Sofia',
        MD: 'Europe/Chisinau',
        UA: 'Europe/Kiev',
        TR: 'Europe/Istanbul',
        BY: 'Europe/Minsk',
        GE: 'Asia/Tbilisi',

        // Africa
        MA: 'Africa/Casablanca', EH: 'Africa/Casablanca',
        DZ: 'Africa/Algiers',
        TN: 'Africa/Tunis',
        LY: 'Africa/Tripoli',
        EG: 'Africa/Cairo',
        NG: 'Africa/Lagos', CM: 'Africa/Lagos', GA: 'Africa/Lagos',
        GQ: 'Africa/Lagos', CF: 'Africa/Lagos', TD: 'Africa/Lagos',
        CG: 'Africa/Lagos', BJ: 'Africa/Lagos',
        TG: 'Africa/Lagos', NE: 'Africa/Lagos',
        GH: 'Africa/Accra', CI: 'Africa/Accra', SN: 'Africa/Accra',
        ML: 'Africa/Accra', BF: 'Africa/Accra', GM: 'Africa/Accra',
        GN: 'Africa/Accra', GW: 'Africa/Accra', SL: 'Africa/Accra',
        LR: 'Africa/Accra', MR: 'Africa/Accra', CV: 'Atlantic/Cape_Verde',
        SD: 'Africa/Khartoum', SS: 'Africa/Juba',
        KE: 'Africa/Nairobi', ET: 'Africa/Addis_Ababa',
        TZ: 'Africa/Dar_es_Salaam',
        UG: 'Africa/Kampala',
        RW: 'Africa/Nairobi', BI: 'Africa/Nairobi',
        SO: 'Africa/Nairobi', DJ: 'Africa/Nairobi', ER: 'Africa/Nairobi',
        KM: 'Africa/Nairobi', YT: 'Africa/Nairobi',
        ZA: 'Africa/Johannesburg', LS: 'Africa/Johannesburg', SZ: 'Africa/Johannesburg',
        BW: 'Africa/Johannesburg', ZW: 'Africa/Johannesburg',
        ZM: 'Africa/Johannesburg', MW: 'Africa/Johannesburg',
        MZ: 'Africa/Maputo',
        NA: 'Africa/Windhoek',
        AO: 'Africa/Lagos',
        MG: 'Africa/Nairobi',
        MU: 'Indian/Mauritius', RE: 'Indian/Reunion', SC: 'Indian/Mauritius',
        SH: 'Africa/Accra', ST: 'Africa/Accra',

        // Middle East
        LB: 'Asia/Beirut',
        IL: 'Asia/Jerusalem', PS: 'Asia/Jerusalem',
        JO: 'Asia/Amman',
        SY: 'Asia/Damascus',
        IQ: 'Asia/Baghdad',
        KW: 'Asia/Kuwait',
        SA: 'Asia/Riyadh', YE: 'Asia/Aden', BH: 'Asia/Bahrain',
        QA: 'Asia/Qatar',
        IR: 'Asia/Tehran',
        AE: 'Asia/Dubai', OM: 'Asia/Muscat',
        AZ: 'Asia/Baku',
        AM: 'Asia/Yerevan',

        // Central Asia
        AF: 'Asia/Kabul',
        PK: 'Asia/Karachi',
        UZ: 'Asia/Tashkent',
        TJ: 'Asia/Dushanbe',
        TM: 'Asia/Ashgabat',
        KG: 'Asia/Bishkek',

        // South Asia
        IN: 'Asia/Kolkata',
        LK: 'Asia/Colombo',
        NP: 'Asia/Kathmandu',
        BD: 'Asia/Dhaka',
        BT: 'Asia/Thimphu',
        MV: 'Indian/Maldives',

        // Southeast Asia
        TH: 'Asia/Bangkok',
        VN: 'Asia/Ho_Chi_Minh',
        LA: 'Asia/Vientiane', KH: 'Asia/Phnom_Penh',
        MM: 'Asia/Yangon',
        SG: 'Asia/Singapore',
        MY: 'Asia/Kuala_Lumpur',
        PH: 'Asia/Manila',
        BN: 'Asia/Brunei',
        TL: 'Asia/Makassar',

        // East Asia
        CN: 'Asia/Shanghai',
        HK: 'Asia/Hong_Kong',
        TW: 'Asia/Taipei',
        MO: 'Asia/Shanghai',
        MN: 'Asia/Ulaanbaatar',
        JP: 'Asia/Tokyo',
        KR: 'Asia/Seoul', KP: 'Asia/Pyongyang',

        // Pacific / Oceania
        NZ: 'Pacific/Auckland',
        FJ: 'Pacific/Fiji',
        PG: 'Pacific/Port_Moresby',
        SB: 'Pacific/Guadalcanal',
        VU: 'Pacific/Noumea',
        NC: 'Pacific/Noumea',
        NF: 'Pacific/Norfolk',
        GU: 'Pacific/Guam', MP: 'Pacific/Guam',
        PW: 'Pacific/Palau',
        MH: 'Pacific/Kwajalein',
        FM: 'Pacific/Guam',
        NR: 'Pacific/Tarawa',
        KI: 'Pacific/Tarawa',
        WS: 'Pacific/Apia',
        TO: 'Pacific/Tongatapu',
        TV: 'Pacific/Tarawa',
        AS: 'Pacific/Pago_Pago',
        CK: 'Pacific/Rarotonga',
        PF: 'Pacific/Tahiti',
        PN: 'Pacific/Gambier',
        NU: 'Pacific/Apia',
        TK: 'Pacific/Apia',
        WF: 'Pacific/Apia',

        // Remote territories
        IO: 'Asia/Dhaka',          // British Indian Ocean Territory (UTC+6)
        CC: 'Asia/Yangon',         // Cocos Islands (UTC+6:30)
        CX: 'Asia/Bangkok',        // Christmas Island (UTC+7)
        TF: 'Indian/Maldives',     // French Southern Territories (UTC+5)
        GS: 'Atlantic/South_Georgia', // South Georgia (UTC-2)
        BV: 'Europe/Oslo',         // Bouvet Island (uninhabited, Norwegian)
        HM: 'Indian/Maldives',     // Heard Island (uninhabited, Australian)
        GO: 'Africa/Nairobi',      // Glorioso Islands
        JU: 'Africa/Nairobi'       // Juan de Nova Island
    };

    // Multi-timezone countries: clicking filters the dropdown to these options
    const multiTzCountries: Record<string, string[]> = {
        US: [
            'America/New_York', 'America/Chicago', 'America/Denver',
            'America/Los_Angeles', 'America/Phoenix', 'America/Anchorage',
            'America/Adak', 'Pacific/Honolulu'
        ],
        CA: [
            'America/St_Johns', 'America/Halifax', 'America/Toronto',
            'America/Winnipeg', 'America/Regina', 'America/Edmonton',
            'America/Vancouver'
        ],
        RU: [
            'Europe/Kaliningrad', 'Europe/Moscow', 'Europe/Samara',
            'Europe/Astrakhan', 'Europe/Volgograd', 'Europe/Saratov',
            'Asia/Yekaterinburg', 'Asia/Omsk', 'Asia/Novosibirsk',
            'Asia/Krasnoyarsk', 'Asia/Irkutsk', 'Asia/Yakutsk',
            'Asia/Vladivostok', 'Asia/Magadan', 'Asia/Sakhalin',
            'Asia/Kamchatka', 'Asia/Anadyr'
        ],
        AU: [
            'Australia/Perth', 'Australia/Eucla', 'Australia/Darwin',
            'Australia/Adelaide', 'Australia/Brisbane', 'Australia/Sydney',
            'Australia/Melbourne', 'Australia/Hobart', 'Australia/Lord_Howe'
        ],
        BR: [
            'America/Sao_Paulo', 'America/Manaus', 'America/Cuiaba',
            'America/Rio_Branco', 'America/Fortaleza', 'America/Noronha'
        ],
        MX: [
            'America/Mexico_City', 'America/Mazatlan',
            'America/Tijuana', 'America/Cancun'
        ],
        CL: [
            'America/Santiago', 'Pacific/Easter'
        ],
        CD: [
            'Africa/Lagos', 'Africa/Maputo'
        ],
        KZ: [
            'Asia/Almaty', 'Asia/Omsk' // West KZ is UTC+5, East is UTC+6
        ],
        ID: [
            'Asia/Jakarta', 'Asia/Makassar', 'Asia/Jayapura'
        ]
    };

    // Build reverse lookup: timezone → label from dropdown options
    const tzToLabel: Record<string, string> = {};
    select.querySelectorAll('option').forEach(opt => {
        if (opt.value && opt.value !== 'UTC') {
            tzToLabel[opt.value] = opt.textContent?.trim() ?? opt.value;
        }
    });

    let svgDoc: Document | null = null;

    function getCountryId(el: SVGElement): string | null {
        return el.id || el.closest('[id]')?.id || null;
    }

    /** Get all timezones for a country: multi-tz list if defined, otherwise the single mapping. */
    function getTimezonesForCountry(countryId: string): string[] {
        if (multiTzCountries[countryId]) return multiTzCountries[countryId];
        const tz = countryToTz[countryId];
        return tz ? [tz] : [];
    }

    /** Reverse lookup: find which country ID owns the given timezone. */
    function getCountryForTz(tz: string): string | null {
        // Check multi-tz countries first
        for (const [countryId, tzList] of Object.entries(multiTzCountries)) {
            if (tzList.includes(tz)) return countryId;
        }
        // Then single-tz mapping
        for (const [countryId, countryTz] of Object.entries(countryToTz)) {
            if (countryTz === tz) return countryId;
        }
        return null;
    }

    function setCountryClass(countryId: string, className: string, add: boolean) {
        if (!svgDoc) return;
        const el = svgDoc.getElementById(countryId);
        el?.classList.toggle(className, add);
    }

    function clearAllHighlights() {
        if (!svgDoc) return;
        svgDoc.querySelectorAll<SVGPathElement>('path[id]').forEach(p => {
            p.classList.remove('tz-hover', 'tz-active');
        });
    }

    function highlightCountryForCurrentTz() {
        if (!select || !svgDoc) return;
        const currentTz = select.value;
        if (!currentTz || currentTz === 'UTC') return;

        const countryId = getCountryForTz(currentTz);
        if (countryId) setCountryClass(countryId, 'tz-active', true);
    }

    function showAllTimezones() {
        if (!select) return;
        const utcOption = select.querySelector<HTMLOptionElement>('option[value="UTC"]');
        if (utcOption) utcOption.hidden = false;

        select.querySelectorAll('optgroup').forEach(og => {
            og.hidden = false;
            og.querySelectorAll('option').forEach(opt => {
                (opt as HTMLOptionElement).hidden = false;
            });
        });

        showAllBtn?.classList.add('sn-hidden');
    }

    function filterToTimezones(tzList: string[]) {
        if (!select) return;
        const tzSet = new Set(tzList);

        const utcOption = select.querySelector<HTMLOptionElement>('option[value="UTC"]');
        if (utcOption) utcOption.hidden = true;

        select.querySelectorAll('optgroup').forEach(og => {
            let hasVisible = false;
            og.querySelectorAll('option').forEach(opt => {
                const show = tzSet.has((opt as HTMLOptionElement).value);
                (opt as HTMLOptionElement).hidden = !show;
                if (show) hasVisible = true;
            });
            og.hidden = !hasVisible;
        });

        showAllBtn?.classList.remove('sn-hidden');
    }

    function selectTimezone(tz: string) {
        if (!select) return;

        const option = select.querySelector<HTMLOptionElement>(`option[value="${tz}"]`);
        if (!option) return;

        select.value = tz;
        select.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function handleCountryClick(countryId: string) {
        const tzList = getTimezonesForCountry(countryId);
        if (tzList.length === 0) return;

        clearAllHighlights();
        setCountryClass(countryId, 'tz-active', true);

        const first = tzList[0]!;
        if (tzList.length === 1) {
            // Single timezone — select it, show full dropdown
            showAllTimezones();
            selectTimezone(first);
        } else {
            // Multiple timezones — select first, filter dropdown
            filterToTimezones(tzList);
            selectTimezone(first);
        }
    }

    // --- Inject styles into the SVG document ---

    function injectStyles(doc: Document) {
        const style = doc.createElementNS('http://www.w3.org/2000/svg', 'style');
        style.textContent = `
            path[id] {
                fill: var(--tz-land, #c8d8cf);
                stroke: var(--tz-stroke, #ffffff);
                stroke-width: 0.4;
                transition: fill 0.3s ease;
                cursor: pointer;
            }
            path[id]:hover {
                fill: var(--tz-hover, #6ab893);
            }
            path.tz-hover {
                fill: var(--tz-hover, #6ab893);
            }
            path.tz-active {
                fill: var(--tz-active, #3d9a6c);
            }
        `;
        const svg = doc.querySelector('svg');
        svg?.insertBefore(style, svg.firstChild);
    }

    function syncThemeVars(doc: Document) {
        const svg = doc.querySelector('svg');
        if (!svg) return;

        const cs = getComputedStyle(document.documentElement);

        const borderSubtle = cs.getPropertyValue('--border-subtle').trim() || '#C5D1C5';
        const bgTertiary = cs.getPropertyValue('--bg-tertiary').trim() || '#ffffff';
        const linkPrimary = cs.getPropertyValue('--sn-link-primary').trim() || '#3d9a6c';
        const linkHover = cs.getPropertyValue('--sn-link-hover').trim() || '#145A41';

        svg.style.setProperty('--tz-land', borderSubtle);
        svg.style.setProperty('--tz-stroke', bgTertiary);
        svg.style.setProperty('--tz-hover', linkPrimary);
        svg.style.setProperty('--tz-active', linkHover);

        svg.style.background = bgTertiary;
    }

    // --- Zoom ---

    let origViewBox = { x: 0, y: 0, w: 0, h: 0 };
    let curViewBox = { x: 0, y: 0, w: 0, h: 0 };
    let targetViewBox = { x: 0, y: 0, w: 0, h: 0 };
    const MIN_ZOOM = 1;
    const MAX_ZOOM = 6;
    let zoomLevel = 1;
    let animFrame: number | null = null;
    const LERP_SPEED = 0.18;

    function getSvg(): SVGSVGElement | null {
        return svgDoc?.querySelector('svg') ?? null;
    }

    function applyViewBox() {
        const svg = getSvg();
        if (!svg) return;
        svg.setAttribute('viewBox', `${curViewBox.x} ${curViewBox.y} ${curViewBox.w} ${curViewBox.h}`);
    }

    /** Immediately set viewBox without animation (used by pan). */
    function applyViewBoxImmediate() {
        targetViewBox = { ...curViewBox };
        applyViewBox();
    }

    function animateViewBox() {
        const dx = targetViewBox.x - curViewBox.x;
        const dy = targetViewBox.y - curViewBox.y;
        const dw = targetViewBox.w - curViewBox.w;
        const dh = targetViewBox.h - curViewBox.h;

        if (Math.abs(dx) < 0.1 && Math.abs(dy) < 0.1 && Math.abs(dw) < 0.1 && Math.abs(dh) < 0.1) {
            curViewBox = { ...targetViewBox };
            applyViewBox();
            animFrame = null;
            return;
        }

        curViewBox.x += dx * LERP_SPEED;
        curViewBox.y += dy * LERP_SPEED;
        curViewBox.w += dw * LERP_SPEED;
        curViewBox.h += dh * LERP_SPEED;
        applyViewBox();
        animFrame = requestAnimationFrame(animateViewBox);
    }

    function startAnimation() {
        if (animFrame === null) {
            animFrame = requestAnimationFrame(animateViewBox);
        }
    }

    function resetZoom() {
        zoomLevel = 1;
        targetViewBox = { ...origViewBox };
        startAnimation();
    }

    function handleWheel(e: WheelEvent) {
        e.preventDefault();
        const svg = getSvg();
        if (!svg) return;

        const direction = e.deltaY < 0 ? 1 : -1;
        const step = 0.3;
        const newZoom = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoomLevel + direction * step));
        if (newZoom === zoomLevel) return;

        // Mouse position as fraction of SVG viewport
        const svgEl = svg as unknown as Element;
        const mx = e.clientX / svgEl.clientWidth;
        const my = e.clientY / svgEl.clientHeight;

        // Use current animated position for smooth chained zooms
        const svgX = curViewBox.x + mx * curViewBox.w;
        const svgY = curViewBox.y + my * curViewBox.h;

        const newW = origViewBox.w / newZoom;
        const newH = origViewBox.h / newZoom;

        zoomLevel = newZoom;

        if (zoomLevel <= 1) {
            targetViewBox = { ...origViewBox };
        } else {
            targetViewBox = {
                x: svgX - mx * newW,
                y: svgY - my * newH,
                w: newW,
                h: newH
            };
        }

        startAnimation();
    }

    // --- Pan (drag) ---

    let isPanning = false;
    let panStart = { x: 0, y: 0 };
    let viewBoxAtPanStart = { x: 0, y: 0, w: 0, h: 0 };
    let didDrag = false;

    function handlePointerDown(e: PointerEvent) {
        if (zoomLevel <= 1) return;
        isPanning = true;
        didDrag = false;
        panStart = { x: e.clientX, y: e.clientY };
        viewBoxAtPanStart = { ...curViewBox };
        const svg = getSvg();
        if (svg) svg.style.cursor = 'grabbing';
    }

    function handlePointerMove(e: PointerEvent) {
        if (!isPanning) return;
        const svg = getSvg();
        if (!svg) return;

        const svgEl = svg as unknown as Element;
        const dx = (e.clientX - panStart.x) / svgEl.clientWidth * curViewBox.w;
        const dy = (e.clientY - panStart.y) / svgEl.clientHeight * curViewBox.h;

        if (Math.abs(e.clientX - panStart.x) > 3 || Math.abs(e.clientY - panStart.y) > 3) {
            didDrag = true;
        }

        curViewBox.x = viewBoxAtPanStart.x - dx;
        curViewBox.y = viewBoxAtPanStart.y - dy;
        applyViewBoxImmediate();
    }

    function handlePointerUp() {
        if (!isPanning) return;
        isPanning = false;
        const svg = getSvg();
        if (svg) svg.style.cursor = '';
    }

    // --- Init when SVG loads ---

    function initMap() {
        try {
            svgDoc = obj!.contentDocument;
        } catch (_e) {
            return;
        }
        if (!svgDoc) return;

        injectStyles(svgDoc);
        syncThemeVars(svgDoc);
        highlightCountryForCurrentTz();

        // Store original viewBox for zoom
        const svg = getSvg();
        if (svg) {
            const vb = svg.viewBox.baseVal;
            origViewBox = { x: vb.x, y: vb.y, w: vb.width, h: vb.height };
            curViewBox = { ...origViewBox };
        }

        // Scroll wheel zoom
        svgDoc.addEventListener('wheel', (e: Event) => handleWheel(e as WheelEvent), { passive: false });

        // Pan (drag when zoomed)
        svgDoc.addEventListener('pointerdown', (e: Event) => handlePointerDown(e as PointerEvent));
        svgDoc.addEventListener('pointermove', (e: Event) => handlePointerMove(e as PointerEvent));
        svgDoc.addEventListener('pointerup', () => handlePointerUp());
        svgDoc.addEventListener('pointerleave', () => handlePointerUp());

        // Touch devices get selection via the click handler below; skip the
        // hover highlight + tooltip so first-tap doesn't fire both behaviors.
        const coarsePointer = window.matchMedia('(hover: none)').matches;

        // Hover: highlight only the hovered country
        svgDoc.addEventListener('mouseover', (e: Event) => {
            if (coarsePointer) return;
            const target = e.target as SVGElement;
            const countryId = getCountryId(target);
            if (!countryId) return;

            setCountryClass(countryId, 'tz-hover', true);

            // Tooltip
            const countryName = (target as SVGPathElement).getAttribute('title') ?? '';
            const tzList = getTimezonesForCountry(countryId);
            let label: string;
            if (tzList.length > 1) {
                label = `${countryName} — click to choose timezone`;
            } else if (tzList.length === 1) {
                const tz = tzList[0]!;
                // Only show the city if the timezone belongs to this country
                // (check if the IANA path contains a city name from this country's region)
                const tzLabel = tzToLabel[tz] ?? tz;
                const ownerCountry = getCountryForTz(tz);
                const isOwnTz = ownerCountry === countryId;
                label = countryName
                    ? (isOwnTz ? `${countryName} — ${tzLabel}` : countryName)
                    : tzLabel;
            } else {
                label = countryName;
            }
            if (tooltipEl && label) {
                tooltipEl.textContent = label;
                tooltipEl.classList.remove('sn-hidden');
            }
        });

        svgDoc.addEventListener('mouseout', (e: Event) => {
            if (coarsePointer) return;
            const target = e.target as SVGElement;
            const countryId = getCountryId(target);
            if (!countryId) return;

            setCountryClass(countryId, 'tz-hover', false);
            tooltipEl?.classList.add('sn-hidden');
        });

        svgDoc.addEventListener('mousemove', (e: Event) => {
            if (coarsePointer) return;
            if (!tooltipEl || tooltipEl.classList.contains('sn-hidden')) return;
            const me = e as MouseEvent;
            const objRect = obj!.getBoundingClientRect();
            const wrapperRect = obj!.parentElement!.getBoundingClientRect();
            // clientX/Y from SVG contentDocument are relative to the SVG viewport,
            // so add the object's offset within the page
            tooltipEl.style.left = `${objRect.left - wrapperRect.left + me.clientX + 12}px`;
            tooltipEl.style.top = `${objRect.top - wrapperRect.top + me.clientY - 28}px`;
        });

        // Click: select or filter (ignore if it was a drag)
        svgDoc.addEventListener('click', (e: Event) => {
            if (didDrag) { didDrag = false; return; }
            const target = e.target as SVGElement;
            const countryId = getCountryId(target);
            if (!countryId) return;

            tooltipEl?.classList.add('sn-hidden');
            resetZoom();
            handleCountryClick(countryId);
        });

        const observer = new MutationObserver(() => {
            if (svgDoc) syncThemeVars(svgDoc);
        });
        observer.observe(document.documentElement, {
            attributes: true,
            attributeFilter: ['data-theme']
        });
    }

    // Show all button
    showAllBtn?.addEventListener('click', (e: Event) => {
        e.preventDefault();
        showAllTimezones();
    });

    // Dropdown manual change → update map
    select.addEventListener('change', () => {
        clearAllHighlights();
        highlightCountryForCurrentTz();
    });

    // The select value may be set async by profile JS after the map loads.
    // Poll briefly until the value appears, then highlight.
    let pollCount = 0;
    const pollInterval = setInterval(() => {
        pollCount++;
        if (select.value && select.value !== 'UTC') {
            clearAllHighlights();
            highlightCountryForCurrentTz();
            clearInterval(pollInterval);
        } else if (pollCount > 20) {
            clearInterval(pollInterval);
        }
    }, 250);

    if (obj.contentDocument?.querySelector('svg')) {
        initMap();
    } else {
        obj.addEventListener('load', initMap);
    }
})();
