// Hydroscan ana JS dosyası
// Burada hem offline log analizi hem de canlı mod yönetilecek.

// --------------------------------------------------------------------
// GLOBAL DURUMLAR
// --------------------------------------------------------------------
let logData = [];                 // Tüm zaman serisi STATE kayıtları
let startTimestamp = 0;           // İlk kaydın zaman damgası (epoch saniye)
let thrusterLayout = [];          // İtici geometrisi
let targetPoint = { x: 10.0, y: 15.0 }; // Varsayılan hedef (logdan da güncelleniyor)

// CANLI MOD DURUMU
let liveMode = false;             // Canlı mod açık mı?
let liveTimerId = null;           // setInterval id'si

// Son health snapshot'ı (sensör / health panelinde fallback olarak kullanmak için)
let lastHealthSnapshot = null;

// --------------------------------------------------------------------
// YARDIMCI FONKSİYONLAR
// --------------------------------------------------------------------

// Dizideki minimum değeri güvenle hesapla (NaN vs. temizlenir)
function safeMin(arr) {
    const filtered = (arr || []).filter(isFinite);
    if (filtered.length === 0) return -1.0;
    const minVal = Math.min(...filtered);
    return (minVal < 0 && Math.abs(minVal) > 0.01) ? minVal : -1.0;
}

// Dizideki maksimum değeri güvenle hesapla
function safeMax(arr) {
    const filtered = (arr || []).filter(isFinite);
    if (filtered.length === 0) return 1.0;
    const maxVal = Math.max(...filtered);
    return (maxVal > 0 && maxVal > 0.01) ? maxVal : 1.0;
}

// Backend hiç layout veremezse, frontend tarafında sentetik dairesel yerleşim üret
function buildSyntheticThrusterLayout(count) {
    const layout = [];
    if (!count || count <= 0) return layout;

    const R = 0.6; // görselleştirme için keyfi yarıçap (m)
    for (let i = 0; i < count; i++) {
        const angle = 2 * Math.PI * i / Math.max(count, 1);
        const px = R * Math.cos(angle);
        const py = R * Math.sin(angle);
        layout.push({
            name: `SIM_CH${i}`,
            channel: i,
            pos_x: px,
            pos_y: py,
            pos_z: 0.0,
            dir_x: Math.cos(angle),
            dir_y: Math.sin(angle),
            dir_z: 0.0
        });
    }
    return layout;
}

// Backend’ten gelen tek bir data point içindeki string sayıları float’a çevir
function convertDataPoint(d) {
    const obj = { ...d };
    for (const key in obj) {
        if (!Object.prototype.hasOwnProperty.call(obj, key)) continue;
        const val = obj[key];

        // Sadece string olan ve gerçekten sayıya çevrilebilen alanları çevir
        if (
            typeof val === 'string' &&
            !isNaN(parseFloat(val)) &&
            key !== 'time' &&
            key !== 'limiter' &&
            key !== 'task_name' &&
            key !== 'obs_ahead_status' &&
            key !== 'raw_line'
        ) {
            obj[key] = parseFloat(val);
        }
    }
    return obj;
}

// Basit sayı güvenlik fonksiyonu
function safeNum(val, def = 0) {
    return (typeof val === 'number' && isFinite(val)) ? val : def;
}

// Header'daki canlı durum rozetini güncelle
function setLiveStatusBadge(isRunning) {
    const badge = document.getElementById('liveStatusBadge');
    if (!badge) return;

    if (isRunning && liveMode) {
        badge.textContent = 'LIVE';
        badge.className = 'badge badge-online';
    } else {
        badge.textContent = 'OFFLINE';
        badge.className = 'badge badge-offline';
    }
}

// --------------------------------------------------------------------
// ÖZET KARTLARINI GÜNCELLEME
// --------------------------------------------------------------------

function updateSummaryCards() {
    if (!logData || logData.length === 0) return;

    // Toplam süre
    let totalTime = 0;
    try {
        const t0 = new Date(logData[0].time).getTime() / 1000;
        const t1 = new Date(logData[logData.length - 1].time).getTime() / 1000;
        totalTime = t1 - t0;
    } catch {
        totalTime = 0;
    }

    // Toplam yol (XY düzleminde)
    let totalDist = 0;
    for (let i = 1; i < logData.length; i++) {
        const prev = logData[i - 1];
        const cur = logData[i];
        const dx = safeNum(cur.pos_x) - safeNum(prev.pos_x);
        const dy = safeNum(cur.pos_y) - safeNum(prev.pos_y);
        totalDist += Math.sqrt(dx * dx + dy * dy);
    }

    // Maksimum hız (velocity_mag varsa direkt, yoksa vx,vy,vz'den)
    let maxVel = 0;
    for (const dp of logData) {
        let v = dp.velocity_mag;
        if (typeof v !== 'number' || !isFinite(v)) {
            const vx = safeNum(dp.vel_x);
            const vy = safeNum(dp.vel_y);
            const vz = safeNum(dp.vel_z);
            v = Math.sqrt(vx * vx + vy * vy + vz * vz);
        }
        if (v > maxVel) maxVel = v;
    }

    // Limitör / Engelleme olay sayısı
    let limiterEvents = 0;
    for (const dp of logData) {
        const lim = (dp.limiter || 'NONE').toString().toUpperCase();
        const obs = (dp.obs_ahead_status || '--').toString().toUpperCase();
        if ((lim !== 'NONE' && !lim.includes('FALSE')) || obs === 'ENGEL VAR') {
            limiterEvents++;
        }
    }

    const elTotalTime = document.getElementById('summary_total_time');
    const elTotalDist = document.getElementById('summary_total_distance');
    const elMaxVel = document.getElementById('summary_max_velocity');
    const elLimiter = document.getElementById('summary_limiter_events');

    if (elTotalTime) elTotalTime.textContent = `${totalTime.toFixed(2)} s`;
    if (elTotalDist) elTotalDist.textContent = `${totalDist.toFixed(2)} m`;
    if (elMaxVel) elMaxVel.textContent = `${maxVel.toFixed(3)} m/s`;
    if (elLimiter) elLimiter.textContent = `${limiterEvents}`;
}

// --------------------------------------------------------------------
// SENSÖR & HEALTH PANELLERİNİ GÜNCELLEME
// --------------------------------------------------------------------

// Özet string üreticiler
function summarizeImu(imu) {
    if (!imu || typeof imu !== 'object') return '--';
    const ax = safeNum(imu.ax);
    const ay = safeNum(imu.ay);
    const az = safeNum(imu.az);
    const gx = safeNum(imu.gx);
    const gy = safeNum(imu.gy);
    const gz = safeNum(imu.gz);
    const age = safeNum(imu.age_ms);
    return `a=(${ax.toFixed(2)},${ay.toFixed(2)},${az.toFixed(2)}) m/s² | g=(${gx.toFixed(1)},${gy.toFixed(1)},${gz.toFixed(1)}) °/s | age=${age.toFixed(0)} ms`;
}

function summarizeGps(gps) {
    if (!gps || typeof gps !== 'object') return '--';
    const lat = safeNum(gps.lat);
    const lon = safeNum(gps.lon);
    const fix = gps.fix != null ? gps.fix : '?';
    const hdop = safeNum(gps.hdop);
    const age = safeNum(gps.age_ms);
    return `(${lat.toFixed(5)}, ${lon.toFixed(5)}) | fix=${fix} | hdop=${hdop.toFixed(2)} | age=${age.toFixed(0)} ms`;
}

function summarizeLidar(lidar) {
    if (!lidar || typeof lidar !== 'object') return '--';
    const points = safeNum(lidar.points || lidar.point_count || 0);
    const age = safeNum(lidar.age_ms);
    return `nokta=${points.toFixed(0)} | age=${age.toFixed(0)} ms`;
}

function summarizeCamera(cam) {
    if (!cam || typeof cam !== 'object') return '--';
    const fps = safeNum(cam.fps);
    const n = Array.isArray(cam.objects || cam.detections) ? (cam.objects || cam.detections).length : 0;
    const age = safeNum(cam.age_ms);
    return `fps=${fps.toFixed(1)} | obj=${n} | age=${age.toFixed(0)} ms`;
}

function summarizeHealthStatus(health) {
    if (!health || typeof health !== 'object') return '--';
    const status = (health.status || health.level || health.state || 'UNKNOWN').toString().toUpperCase();
    return status;
}

function summarizeHealthCounters(health) {
    if (!health || typeof health !== 'object') return '0';
    const warn = safeNum(health.warning_count || health.warn || 0);
    const err = safeNum(health.error_count || health.err || 0);
    const total = warn + err;
    return `${total.toFixed(0)} (W:${warn.toFixed(0)}, E:${err.toFixed(0)})`;
}

function summarizeLastEvent(health, eventsArr) {
    // Önce health.last_event / health.events, sonra snapshot.events
    if (health && typeof health === 'object') {
        if (typeof health.last_event === 'string' && health.last_event.length > 0) {
            return health.last_event;
        }
        if (Array.isArray(health.events) && health.events.length > 0) {
            const ev = health.events[health.events.length - 1];
            if (typeof ev === 'string') return ev;
            if (ev && typeof ev === 'object') {
                return (ev.message || ev.msg || JSON.stringify(ev)).toString();
            }
        }
    }

    if (Array.isArray(eventsArr) && eventsArr.length > 0) {
        const ev = eventsArr[eventsArr.length - 1];
        if (typeof ev === 'string') return ev;
        if (ev && typeof ev === 'object') {
            return (ev.message || ev.msg || JSON.stringify(ev)).toString();
        }
    }

    return '--';
}

/**
 * frame.sensor_snapshot yapısını parsing.py ile uyumlu şekilde çözer:
 * {
 *   samples: {
 *      imu: { data: {...}, quality: {...}, ... },
 *      gps: { ... },
 *      camera: { ... },
 *      ...
 *   },
 *   laserscan: { data: {...}, quality: {...}, ... },
 *   fused_state: {...},
 *   external_state: {...},
 *   health: {...},
 *   events: [...]
 * }
 */
function updateSensorAndHealthPanels(frame) {
    // parsing.py → STATE içinde: "sensor_snapshot": { ... }
    const snapshot = frame.sensor_snapshot || frame.sensors_snapshot || {};
    const samples = snapshot.samples || {};
    const laserscan = snapshot.laserscan || null;
    const healthFromSnapshot = snapshot.health || null;
    const eventsFromSnapshot = snapshot.events || [];

    // --- IMU ---
    const imuSample = samples.imu || samples.IMU || null;
    const imuData = (imuSample && imuSample.data) || {};
    const imuQuality = (imuSample && imuSample.quality) || {};

    const imu = {
        ax: imuData.ax,
        ay: imuData.ay,
        az: imuData.az,
        gx: imuData.gx,
        gy: imuData.gy,
        gz: imuData.gz,
        age_ms: imuQuality.age_ms != null ? imuQuality.age_ms : imuData.age_ms
    };

    // --- GPS ---
    const gpsSample = samples.gps || samples.GPS || null;
    const gpsData = (gpsSample && gpsSample.data) || {};
    const gpsQuality = (gpsSample && gpsSample.quality) || {};

    const gps = {
        lat: gpsData.lat,
        lon: gpsData.lon,
        alt: gpsData.alt,
        fix: gpsData.fix,
        hdop: gpsData.hdop,
        age_ms: gpsQuality.age_ms != null ? gpsQuality.age_ms : gpsData.age_ms
    };

    // --- LiDAR (LaserScan) ---
    const lidarData = (laserscan && laserscan.data) || {};
       const lidarQuality = (laserscan && laserscan.quality) || {};
    const lidar = {
        points: Array.isArray(lidarData.ranges) ? lidarData.ranges.length : (lidarData.points || lidarData.point_count),
        age_ms: lidarQuality.age_ms != null ? lidarQuality.age_ms : lidarData.age_ms
    };

    // --- Kamera ---
    const camSample = samples.camera || samples.cam || samples.CAMERA || samples.CAM || null;
    const camData = (camSample && camSample.data) || {};
    const camQuality = (camSample && camSample.quality) || {};

    const camera = {
        fps: camData.fps,
        objects: camData.objects || camData.detections,
        age_ms: camQuality.age_ms != null ? camQuality.age_ms : camData.age_ms
    };

    // --- Sağlık ---
    const health = frame.health_snapshot || healthFromSnapshot || lastHealthSnapshot || {};
    const lastEventText = summarizeLastEvent(health, eventsFromSnapshot);

    // DOM elemanları
    const elImu = document.getElementById('sensor_imu_status');
    const elGps = document.getElementById('sensor_gps_status');
    const elCam = document.getElementById('sensor_cam_status');
    const elLidar = document.getElementById('sensor_lidar_status');

    if (elImu) elImu.textContent = summarizeImu(imu);
    if (elGps) elGps.textContent = summarizeGps(gps);
    if (elCam) elCam.textContent = summarizeCamera(camera);
    if (elLidar) elLidar.textContent = summarizeLidar(lidar);

    // Sağlık panelleri
    const elRuntime = document.getElementById('health_runtime_status');
    const elWarnCount = document.getElementById('health_warning_count');
    const elLastEvent = document.getElementById('health_last_event');

    if (elRuntime) elRuntime.textContent = summarizeHealthStatus(health);
    if (elWarnCount) elWarnCount.textContent = summarizeHealthCounters(health);
    if (elLastEvent) elLastEvent.textContent = lastEventText;

    // Son health’i cache’le (bir sonraki frame health_snapshot içermese de paneller boş kalmasın)
    if (health && Object.keys(health).length > 0) {
        lastHealthSnapshot = { ...health };
    }

    // Sensör bölümü görünür olsun
    $('#sensorSection').show();
}

// Sensör & Health grafiklerini gerçek veriyle çiz
function drawSensorAndHealthPlots() {
    const sensorPlotDiv = document.getElementById('sensorRatePlot');
    const healthPlotDiv = document.getElementById('healthTimelinePlot');
    if (!sensorPlotDiv || !healthPlotDiv || !logData || logData.length === 0) return;

    // -----------------------------
    // 1) Sensör oranları (Hz) için seriler
    // -----------------------------
    const imuTimes = [], imuRates = [];
    const gpsTimes = [], gpsRates = [];
    const lidarTimes = [], lidarRates = [];
    const camTimes = [], camRates = [];

    const statusToLevel = (statusRaw) => {
        const s = (statusRaw || '').toString().toUpperCase();
        if (!s) return null;
        if (s.includes('OK') || s.includes('HEALTHY') || s === 'GOOD') return 0;
        if (s.includes('WARN') || s.includes('WARNING')) return 1;
        if (s.includes('ERR') || s.includes('ERROR') || s.includes('CRIT') || s.includes('FAULT')) return 2;
        return null;
    };

    const healthTimes = [];
    const healthLevels = [];

    for (const dp of logData) {
        if (!dp.time) continue;

        const t = (new Date(dp.time).getTime() / 1000) - startTimestamp;
        const snap = dp.sensor_snapshot || dp.sensors_snapshot || {};
        const samples = snap.samples || {};
        const laserscan = snap.laserscan || null;

        // ---------- IMU ----------
        const imuSample = samples.imu || samples.IMU || null;
        if (imuSample) {
            const imuQ = imuSample.quality || {};
            const imuD = imuSample.data || {};
            let rate = (typeof imuQ.rate_hz === 'number') ? imuQ.rate_hz : null;

            if ((rate == null || !isFinite(rate)) && (typeof imuQ.age_ms === 'number' && imuQ.age_ms > 0)) {
                rate = 1000.0 / imuQ.age_ms;
            }
            if ((rate == null || !isFinite(rate)) && (typeof imuD.age_ms === 'number' && imuD.age_ms > 0)) {
                rate = 1000.0 / imuD.age_ms;
            }

            if (typeof rate === 'number' && isFinite(rate) && rate >= 0) {
                imuTimes.push(t);
                imuRates.push(rate);
            }
        }

        // ---------- GPS ----------
        const gpsSample = samples.gps || samples.GPS || null;
        if (gpsSample) {
            const gpsQ = gpsSample.quality || {};
            const gpsD = gpsSample.data || {};
            let rate = (typeof gpsQ.rate_hz === 'number') ? gpsQ.rate_hz : null;

            if ((rate == null || !isFinite(rate)) && (typeof gpsQ.age_ms === 'number' && gpsQ.age_ms > 0)) {
                rate = 1000.0 / gpsQ.age_ms;
            }
            if ((rate == null || !isFinite(rate)) && (typeof gpsD.age_ms === 'number' && gpsD.age_ms > 0)) {
                rate = 1000.0 / gpsD.age_ms;
            }

            if (typeof rate === 'number' && isFinite(rate) && rate >= 0) {
                gpsTimes.push(t);
                gpsRates.push(rate);
            }
        }

        // ---------- LiDAR (LaserScan) ----------
        if (laserscan) {
            const lq = laserscan.quality || {};
            const ld = laserscan.data || {};
            let rate = (typeof lq.rate_hz === 'number') ? lq.rate_hz : null;

            if ((rate == null || !isFinite(rate)) && (typeof lq.age_ms === 'number' && lq.age_ms > 0)) {
                rate = 1000.0 / lq.age_ms;
            }
            if ((rate == null || !isFinite(rate)) && (typeof ld.age_ms === 'number' && ld.age_ms > 0)) {
                rate = 1000.0 / ld.age_ms;
            }

            if (typeof rate === 'number' && isFinite(rate) && rate >= 0) {
                lidarTimes.push(t);
                lidarRates.push(rate);
            }
        }

        // ---------- Kamera ----------
        const camSample = samples.camera || samples.cam || samples.CAMERA || samples.CAM || null;
        if (camSample) {
            const cq = camSample.quality || {};
            const cd = camSample.data || {};
            // Kamera için kalite rate_hz varsa onu al, yoksa fps'i oran kabul et
            let rate = (typeof cq.rate_hz === 'number') ? cq.rate_hz : null;
            if (rate == null || !isFinite(rate)) {
                if (typeof cd.fps === 'number' && isFinite(cd.fps)) {
                    rate = cd.fps;
                }
            }
            if ((rate == null || !isFinite(rate)) && (typeof cq.age_ms === 'number' && cq.age_ms > 0)) {
                rate = 1000.0 / cq.age_ms;
            }
            if ((rate == null || !isFinite(rate)) && (typeof cd.age_ms === 'number' && cd.age_ms > 0)) {
                rate = 1000.0 / cd.age_ms;
            }

            if (typeof rate === 'number' && isFinite(rate) && rate >= 0) {
                camTimes.push(t);
                camRates.push(rate);
            }
        }

        // ---------- Health ----------
        const healthSnap = dp.health_snapshot || snap.health || null;
        if (healthSnap) {
            const statusRaw = healthSnap.status || healthSnap.level || healthSnap.state || '';
            const level = statusToLevel(statusRaw);
            if (level !== null) {
                healthTimes.push(t);
                healthLevels.push(level);
            }
        }
    }

    const anySensor =
        imuTimes.length > 0 ||
        gpsTimes.length > 0 ||
        lidarTimes.length > 0 ||
        camTimes.length > 0;

    const anyHealth = healthTimes.length > 0;

    // -----------------------------
    // 2) Sensör oran grafiği
    // -----------------------------
    if (!anySensor) {
        Plotly.newPlot('sensorRatePlot', [{
            x: [0],
            y: [0],
            mode: 'text',
            text: 'Sensör zaman serisi kaydı bulunamadı.',
            type: 'scatter'
        }], {
            xaxis: { visible: false },
            yaxis: { visible: false },
            margin: { t: 20, r: 10, b: 10, l: 10 }
        }, { responsive: true, displayModeBar: false });
    } else {
        const traces = [];
        if (imuTimes.length) {
            traces.push({
                x: imuTimes,
                y: imuRates,
                mode: 'lines',
                name: 'IMU',
                line: { width: 2 }
            });
        }
        if (gpsTimes.length) {
            traces.push({
                x: gpsTimes,
                y: gpsRates,
                mode: 'lines',
                name: 'GPS',
                line: { width: 2, dash: 'dot' }
            });
        }
        if (lidarTimes.length) {
            traces.push({
                x: lidarTimes,
                y: lidarRates,
                mode: 'lines',
                name: 'LiDAR',
                line: { width: 2, dash: 'dash' }
            });
        }
        if (camTimes.length) {
            traces.push({
                x: camTimes,
                y: camRates,
                mode: 'lines',
                name: 'Kamera',
                line: { width: 2, dash: 'dashdot' }
            });
        }

        const allRates = []
            .concat(imuRates, gpsRates, lidarRates, camRates)
            .filter(v => typeof v === 'number' && isFinite(v) && v >= 0);
        const maxRate = allRates.length ? Math.max(...allRates) : 1.0;

        Plotly.newPlot('sensorRatePlot', traces, {
            title: 'Sensör Veri Oranları (Hz)',
            xaxis: { title: 'Zaman (s)' },
            yaxis: {
                title: 'Efektif Oran (Hz)',
                rangemode: 'tozero',
                range: [0, maxRate * 1.2]
            },
            hovermode: 'x unified',
            margin: { t: 40, r: 20, b: 40, l: 50 }
        }, { responsive: true, displayModeBar: false });
    }

    // -----------------------------
    // 3) Health / Event zaman çizgisi
    // -----------------------------
    if (!anyHealth) {
        Plotly.newPlot('healthTimelinePlot', [{
            x: [0],
            y: [0],
            mode: 'text',
            text: 'Health/Event zaman serisi kaydı bulunamadı.',
            type: 'scatter'
        }], {
            xaxis: { visible: false },
            yaxis: { visible: false },
            margin: { t: 20, r: 10, b: 10, l: 10 }
        }, { responsive: true, displayModeBar: false });
    } else {
        Plotly.newPlot('healthTimelinePlot', [{
            x: healthTimes,
            y: healthLevels,
            mode: 'lines+markers',
            name: 'Health Seviyesi',
            line: { shape: 'hv', width: 2 }
        }], {
            title: 'Health / Event Zaman Çizgisi',
            xaxis: { title: 'Zaman (s)' },
            yaxis: {
                title: 'Health Seviyesi',
                tickmode: 'array',
                tickvals: [0, 1, 2],
                ticktext: ['OK', 'WARN', 'ERROR'],
                range: [-0.5, 2.5]
            },
            hovermode: 'x unified',
            margin: { t: 40, r: 20, b: 40, l: 60 }
        }, { responsive: true, displayModeBar: false });
    }
}


// --------------------------------------------------------------------
// OFFLINE LOG YÜKLEME (Dosyadan)
// --------------------------------------------------------------------

function uploadFile() {
    const fileInput = document.getElementById('logFile');
    const file = fileInput.files[0];
    if (!file) {
        alert("Lütfen bir log dosyası seçiniz.");
        return;
    }

    // Her ihtimale karşı canlı modu kapat
    stopLiveMode(false);

    const formData = new FormData();
    formData.append('logFile', file);

    $('#controlsSection').hide();
    $('#plotsContainer').hide();
    $('#sensorSection').hide();
    $('#logConsole').text("Analiz ediliyor, lütfen bekleyin...");
    setLiveStatusBadge(false);

    $.ajax({
        url: '/upload-log',
        type: 'POST',
        data: formData,
        processData: false,
        contentType: false,
        success: function (response) {
            if (response.success) {
                let data = response.data || [];

                if (data.length > 0) {
                    try {
                        // Gelen her kayıt için tip temizliği yap
                        logData = data.map(convertDataPoint);

                        // İlk zaman damgasını referans al
                        startTimestamp = new Date(logData[0].time).getTime() / 1000;

                        // Hedef noktayı log içinden çek (ilk valid target_x/target_y)
                        const firstDataPoint = logData.find(d =>
                            d.target_x !== null &&
                            d.target_y !== null &&
                            !isNaN(d.target_x) &&
                            !isNaN(d.target_y)
                        );
                        if (firstDataPoint) {
                            targetPoint = {
                                x: firstDataPoint.target_x,
                                y: firstDataPoint.target_y
                            };
                        }

                        // --- Thruster layout ve sayısını backend + fallback ile bul ---
                        let thrusterCount = 0;

                        if (typeof response.thruster_count === 'number' && response.thruster_count > 0) {
                            thrusterCount = response.thruster_count;
                        }

                        // Backend'den gelen layout
                        if (Array.isArray(response.thruster_layout)) {
                            thrusterLayout = response.thruster_layout;
                        } else {
                            thrusterLayout = [];
                        }

                        // Eğer backend hem count hem layout veremediyse actuator uzunluğundan tahmin et
                        if (!thrusterCount || thrusterCount === 0) {
                            let maxActLen = 0;
                            for (const dp of logData) {
                                const arr = Array.isArray(dp.actuators) ? dp.actuators : [];
                                if (arr.length > maxActLen) maxActLen = arr.length;
                            }
                            thrusterCount = maxActLen;
                        }

                        // Eğer hâlâ layout yok ama thrusterCount > 0 ise, frontend sentetik layout üretiyor
                        if ((!thrusterLayout || thrusterLayout.length === 0) && thrusterCount > 0) {
                            thrusterLayout = buildSyntheticThrusterLayout(thrusterCount);
                            console.log("Frontend sentetik thruster layout üretti:", thrusterLayout);
                        }

                        // Son emniyet: thrusterCount 0 ise layout uzunluğundan set et
                        if ((!thrusterCount || thrusterCount === 0) && Array.isArray(thrusterLayout)) {
                            thrusterCount = thrusterLayout.length;
                        }

                        // Kontrolleri hazırla ve grafik çiz
                        initializeControls(thrusterCount);
                        drawPlots();
                        drawSensorAndHealthPlots();
                        updateSummaryCards();

                        $('#controlsSection').show();
                        $('#plotsContainer').show();
                        $('#sensorSection').show();

                        // Slider’ı başa alıp ilk adımı göster
                        document.getElementById('timeSlider').value = 0;
                        updateVisualization(0);

                        console.log("Log Data Loaded Successfully. Starting visualization.");
                        alert(`Başarıyla ${logData.length} kayıt yüklendi. Analiz paneli hazır.`);
                    } catch (e) {
                        console.error("Görselleştirme Başlatma Hatası:", e);
                        alert("Veri yükleme başarılı, ancak görselleştirme başlatılamadı. Konsol hatalarını kontrol edin.");
                        $('#logConsole').text(`KRİTİK HATA: Görselleştirme başlatılamadı. Detay: ${e.message}`);
                    }
                } else {
                    alert("Log dosyasında analiz edilecek geçerli STATE verisi bulunamadı.");
                }
            } else {
                alert('Hata: ' + response.error);
                console.error("Server Error:", response.error);
            }
        },
        error: function (xhr, status, error) {
            console.error("AJAX Error:", error, xhr.responseText);
            alert("Dosya yüklenirken bir AJAX/Sunucu hatası oluştu. Lütfen konsol çıktısını kontrol edin.");
            $('#logConsole').text(`Hata: ${xhr.status} ${xhr.statusText}. Detaylar için konsola bakın.`);
        }
    });
}

// --------------------------------------------------------------------
// KONTROL BÖLÜMÜ BAŞLATMA
// --------------------------------------------------------------------

function initializeControls(thrusterCount) {
    const slider = document.getElementById('timeSlider');
    slider.max = Math.max(logData.length - 1, 0);

    let totalDuration = 0;
    if (logData.length > 1) {
        const lastTimestamp = new Date(logData[logData.length - 1].time).getTime() / 1000;
        totalDuration = lastTimestamp - startTimestamp;
    }

    document.getElementById('maxSteps').textContent = logData.length;
    document.getElementById('maxTime').textContent = totalDuration.toFixed(2);
    document.getElementById('thrusterCount').textContent = thrusterCount;

    document.querySelector('#taskTarget').textContent =
        `(Hedef: ${targetPoint.x.toFixed(1)}, ${targetPoint.y.toFixed(1)})`;

    // Özet kartları güncelle
    updateSummaryCards();
}

// --------------------------------------------------------------------
// GÖRSELLEŞTİRME – ANLIK ADIM GÜNCELLEME
// --------------------------------------------------------------------

function updateVisualization(step) {
    step = parseInt(step);
    if (logData.length === 0 || step >= logData.length || step < 0) return;

    const currentData = logData[step];
    const getTime = (dateString) => {
        try { return (new Date(dateString).getTime() / 1000) - startTimestamp; } catch { return 0; }
    };
    const currentTime = getTime(currentData.time);

    // 1. Dashboard Güncelleme
    document.getElementById('currentStep').textContent = step;
    document.getElementById('currentTime').textContent = currentTime.toFixed(2);

    document.getElementById('disp_pos_xyz').textContent =
        `${safeNum(currentData.pos_x).toFixed(2)}, ${safeNum(currentData.pos_y).toFixed(2)}, ${safeNum(currentData.pos_z).toFixed(2)}`;

    document.getElementById('disp_vel_xyz').textContent =
        `${safeNum(currentData.vel_x).toFixed(3)}, ${safeNum(currentData.vel_y).toFixed(3)}, ${safeNum(currentData.vel_z).toFixed(3)}`;

    document.getElementById('disp_vel_mag').textContent =
        safeNum(currentData.velocity_mag).toFixed(3);

    document.getElementById('disp_rpy_all').textContent =
        `${safeNum(currentData.rpy_r).toFixed(1)} / ${safeNum(currentData.rpy_p).toFixed(1)} / ${safeNum(currentData.heading_deg).toFixed(1)}`;

    document.getElementById('disp_cmd_tr').textContent =
        `${safeNum(currentData.cmd_thr).toFixed(3)} / ${safeNum(currentData.cmd_rud).toFixed(3)}`;

    document.getElementById('disp_dist').textContent =
        safeNum(currentData.dist_to_target).toFixed(2);

    document.getElementById('disp_torque_xyz').textContent =
        `${safeNum(currentData.torque_x).toFixed(2)} / ${safeNum(currentData.torque_y).toFixed(2)} / ${safeNum(currentData.torque_z).toFixed(2)}`;

    document.getElementById('disp_force_xyz').textContent =
        `${safeNum(currentData.force_x).toFixed(2)} / ${safeNum(currentData.force_y).toFixed(2)} / ${safeNum(currentData.force_z).toFixed(2)}`;

    document.getElementById('taskStatus').textContent =
        (currentData.task_name || '--').toString().toUpperCase();

    // Durum Göstergeleri
    const limiterEl = document.getElementById('limiterStatus');
    const dispLimiter = document.getElementById('disp_limiter');
    const limiterStr = (currentData.limiter || 'NONE').toString().toUpperCase();
    dispLimiter.textContent = limiterStr;
    const isLimiterActive = limiterStr !== 'NONE' && !limiterStr.includes('FALSE');
    limiterEl.className = isLimiterActive ? 'indicator alert-active' : 'indicator alert-inactive';

    const dispObs = document.getElementById('disp_obsAhead');
    const obsEl = document.getElementById('obstacleStatus');
    const obsStr = (currentData.obs_ahead_status || '--').toString().toUpperCase();
    dispObs.textContent = obsStr;

    if (obsStr === 'TEMİZ') {
        obsEl.className = 'indicator alert-inactive';
    } else if (obsStr === 'ENGEL VAR') {
        obsEl.className = 'indicator alert-active';
    } else {
        obsEl.className = 'indicator alert-default';
    }

    // Sensör ve health panelleri
    updateSensorAndHealthPanels(currentData);

    // Actuator değerlerini güvenli çek
    const actuators = Array.isArray(currentData.actuators) ? currentData.actuators : [];
    const ch = (idx) => (typeof actuators[idx] === 'number' && isFinite(actuators[idx]))
        ? actuators[idx].toFixed(3)
        : '---';

    // 2. Log Konsolunu Güncelle
    document.getElementById('logConsole').textContent =
        `[Zaman: ${currentData.time}]\n` +
        `[TASK] ${(currentData.task_name || '--').toString().toUpperCase()} Hedef: ` +
        `(${currentData.target_x != null ? safeNum(currentData.target_x).toFixed(2) : '--'}, ` +
        `${currentData.target_y != null ? safeNum(currentData.target_y).toFixed(2) : '--'}) \n` +
        `[STATE] Pos: (${safeNum(currentData.pos_x).toFixed(3)}, ${safeNum(currentData.pos_y).toFixed(3)}, ${safeNum(currentData.pos_z).toFixed(3)}) ` +
        `RPY: (${safeNum(currentData.rpy_r).toFixed(1)}, ${safeNum(currentData.rpy_p).toFixed(1)}, ${safeNum(currentData.heading_deg).toFixed(1)}) \n` +
        `[VELOCITY] (Vx=${safeNum(currentData.vel_x).toFixed(3)}, Vy=${safeNum(currentData.vel_y).toFixed(3)}, Vz=${safeNum(currentData.vel_z).toFixed(3)}) ` +
        `Mag=${safeNum(currentData.velocity_mag).toFixed(3)}\n` +
        `[CONTROL] Dist=${safeNum(currentData.dist_to_target).toFixed(2)}m, ` +
        `dHead=${safeNum(currentData.dhead_to_target).toFixed(1)}deg. Limitör: ${currentData.limiter}\n` +
        `[FORCE/TORQUE] Fx=${safeNum(currentData.force_x).toFixed(2)}, Fy=${safeNum(currentData.force_y).toFixed(2)}, Fz=${safeNum(currentData.force_z).toFixed(2)} | ` +
        `Tx=${safeNum(currentData.torque_x).toFixed(2)}, Ty=${safeNum(currentData.torque_y).toFixed(2)}, Tz=${safeNum(currentData.torque_z).toFixed(2)}\n` +
        `[ACTUATOR OUT] CH0=${ch(0)}, CH1=${ch(1)}, CH2=${ch(2)}, CH3=${ch(3)}\n\n` +
        `[RAW LINE] ${(currentData.raw_line || '').toString().substring(0, 150)}...`;

    // 3. Grafik İşaretçilerini Güncelle
    try {
        // Araç anlık konum işaretçisini güncelle (trace index 1)
        Plotly.restyle('positionPlot',
            {
                'x': [[safeNum(currentData.pos_x)]],
                'y': [[safeNum(currentData.pos_y)]]
            },
            [1]
        );

        const layoutUpdates = {
            'shapes[0].x0': currentTime,
            'shapes[0].x1': currentTime
        };
        Plotly.relayout('commandPlot', layoutUpdates);
        Plotly.relayout('forceTorquePlot', layoutUpdates);
        Plotly.relayout('rpyPlot', layoutUpdates);

        // 4. İtici Çıkış Grafiğini Güncelle
        updateThrusterLayoutVisualization(actuators);
    } catch (e) {
        console.error("Plotly Güncelleme Hatası:", e);
    }
}

// --------------------------------------------------------------------
// İTİCİ GÖRSELLEŞTİRME FONKSİYONLARI
// --------------------------------------------------------------------

function getThrusterTraces(thrusters, currentOutputs) {
    const traces = [];
    const scale = 0.5;

    // İtici Konumları (Noktalar)
    traces.push({
        x: thrusters.map(t => t.pos_x),
        y: thrusters.map(t => t.pos_y),
        mode: 'markers+text',
        type: 'scatter',
        name: 'İtici Konumları',
        marker: { size: 10, color: '#0f172a' },
        text: thrusters.map(t => t.name || (`CH${t.channel}`)),
        textposition: 'top center'
    });

    const annotations = [];
    thrusters.forEach((t, i) => {
        const outputArr = Array.isArray(currentOutputs) ? currentOutputs : [];
        const rawOut = outputArr[i];
        const safeOutput = (typeof rawOut === 'number' && isFinite(rawOut)) ? rawOut : 0;

        const length = Math.abs(safeOutput) * scale;
        const color = safeOutput > 0 ? '#10b981' : (safeOutput < 0 ? '#ef4444' : '#cbd5e1');

        const end_x = t.pos_x + (t.dir_x || 0) * length;
        const end_y = t.pos_y + (t.dir_y || 0) * length;

        annotations.push({
            x: end_x,
            y: end_y,
            ax: t.pos_x,
            ay: t.pos_y,
            xref: 'x',
            yref: 'y',
            text: '',
            showarrow: true,
            arrowhead: 3,
            arrowsize: 1.5,
            arrowwidth: 3,
            arrowcolor: color
        });
    });

    // Merkez (CoG)
    traces.push({
        x: [0],
        y: [0],
        mode: 'markers',
        name: 'Merkez Kütle',
        marker: { size: 10, symbol: 'x', color: '#f97316' }
    });

    return { traces: traces, annotations: annotations };
}

function updateThrusterLayoutVisualization(currentOutputs) {
    if (!Array.isArray(thrusterLayout) || thrusterLayout.length === 0) {
        // Gerçekten hiç layout yoksa placeholder çiz
        Plotly.newPlot('thrusterLayoutPlot', [{
            x: [0],
            y: [0],
            mode: 'text',
            text: 'İTİCİ GEOMETRİ KAYDI BULUNAMADI',
            type: 'scatter'
        }], {
            title: 'İtici Geometrisi ve Anlık Kuvvet Dağılımı (Gövde Frame)',
            xaxis: { range: [-1.2, 1.2], title: 'Gövde X Ekseni (m)' },
            yaxis: { range: [-1.2, 1.2], title: 'Gövde Y Ekseni (m)', scaleanchor: "x", scaleratio: 1 },
            annotations: [],
            showlegend: false,
            margin: { t: 40, r: 20, b: 30, l: 40 }
        }, { responsive: true, displayModeBar: false });
        return;
    }

    try {
        const { annotations } = getThrusterTraces(thrusterLayout, currentOutputs);
        // Sadece okları güncelle
        Plotly.relayout('thrusterLayoutPlot', { annotations: annotations });
    } catch (e) {
        console.error("İtici Görselleştirme Güncelleme Hatası:", e);
    }
}

// --------------------------------------------------------------------
// GRAFİK ÇİZME (İLK YÜKLEME)
// --------------------------------------------------------------------

function drawPlots() {
    if (logData.length === 0) return;

    const times = logData.map(d => (new Date(d.time).getTime() / 1000) - startTimestamp);

    const pos_x = logData.map(d => d.pos_x);
    const pos_y = logData.map(d => d.pos_y);
    const velocity_mag = logData.map(d => d.velocity_mag);
    const heading = logData.map(d => d.heading_deg);
    const cmd_thr = logData.map(d => d.cmd_thr);
    const cmd_rud = logData.map(d => d.cmd_rud);
    const rpy_r = logData.map(d => d.rpy_r);
    const rpy_p = logData.map(d => d.rpy_p);
    const force_x = logData.map(d => d.force_x);
    const force_y = logData.map(d => d.force_y);
    const force_z = logData.map(d => d.force_z);
    const torque_z = logData.map(d => d.torque_z);
    const torque_x = logData.map(d => d.torque_x);
    const torque_y = logData.map(d => d.torque_y);

    // Global eksen aralıklarını belirle (Görev noktası dahil)
    const x_all = [targetPoint.x, ...pos_x];
    const y_all = [targetPoint.y, ...pos_y];

    const x_range = [safeMin(x_all) - 2, safeMax(x_all) + 2];
    const y_range = [safeMin(y_all) - 2, safeMax(y_all) + 2];

    // Zaman işaretçisi için layout şekli
    const time_marker_shape = {
        type: 'line',
        x0: 0,
        x1: 0,
        y0: 0,
        y1: 1,
        yref: 'paper',
        line: { color: '#dc2626', width: 1.5, dash: 'dash' }
    };

    // --- 1. Pozisyon Grafiği (Yörünge) ---
    try {
        const positionTraces = [
            {
                x: pos_x,
                y: pos_y,
                mode: 'lines',
                type: 'scatter',
                name: 'Rota',
                line: { color: '#3b82f6', width: 2 }
            },
            {
                x: [pos_x[0]],
                y: [pos_y[0]],
                mode: 'markers',
                type: 'scatter',
                name: 'Araç Konumu',
                marker: { color: '#f97316', size: 12, symbol: 'circle' }
            },
            {
                x: [pos_x[0]],
                y: [pos_y[0]],
                mode: 'markers',
                type: 'scatter',
                name: 'Başlangıç',
                marker: { color: '#10b981', size: 8, symbol: 'square' }
            },
            {
                x: [targetPoint.x],
                y: [targetPoint.y],
                mode: 'markers',
                type: 'scatter',
                name: 'Hedef',
                marker: { color: '#ef4444', size: 15, symbol: 'star' }
            }
        ];

        const positionLayout = {
            title: 'Robot Yolu (X-Y Düzlemi)',
            xaxis: { title: 'X Pozisyonu (m)', range: x_range },
            yaxis: { title: 'Y Pozisyonu (m)', range: y_range, scaleanchor: "x", scaleratio: 1 },
            hovermode: 'closest',
            margin: { t: 40, r: 20, b: 30, l: 40 }
        };
        Plotly.newPlot('positionPlot', positionTraces, positionLayout, { responsive: true, displayModeBar: false });
    } catch (e) {
        console.error("Pozisyon Grafiği Çizim Hatası:", e);
    }

    // --- 2. Hız ve Komut Grafiği ---
    try {
        const commandTraces = [
            {
                x: times,
                y: velocity_mag,
                mode: 'lines',
                name: 'Hız Büyüklüğü (m/s)',
                yaxis: 'y1',
                line: { color: '#3b82f6', width: 3 }
            },
            {
                x: times,
                y: cmd_thr,
                mode: 'lines',
                name: 'İtici Komut (Thr)',
                yaxis: 'y2',
                line: { color: '#f97316', width: 1.5, dash: 'dot' }
            },
            {
                x: times,
                y: cmd_rud,
                mode: 'lines',
                name: 'Dümen Komut (Rud)',
                yaxis: 'y2',
                line: { color: '#10b981', width: 1.5, dash: 'dash' }
            }
        ];

        const maxVel = safeMax(velocity_mag) * 1.1;

        const commandLayout = {
            title: 'Hız Büyüklüğü ve Kontrol Komutları',
            xaxis: { title: 'Zaman (s)' },
            yaxis: {
                title: 'Hız (m/s)',
                titlefont: { color: '#3b82f6' },
                tickfont: { color: '#3b82f6' },
                range: [0, maxVel > 1.5 ? maxVel : 1.5]
            },
            yaxis2: {
                title: 'Komut Değeri (unit)',
                overlaying: 'y',
                side: 'right',
                range: [-1.1, 1.1],
                titlefont: { color: '#f97316' },
                tickfont: { color: '#f97316' }
            },
            hovermode: 'x unified',
            margin: { t: 40, r: 40, b: 30, l: 40 },
            shapes: [time_marker_shape]
        };
        Plotly.newPlot('commandPlot', commandTraces, commandLayout, { responsive: true, displayModeBar: false });
    } catch (e) {
        console.error("Komut Grafiği Çizim Hatası:", e);
    }

    // --- 3. Roll/Pitch/Yaw Grafiği ---
    try {
        const rpyTraces = [
            {
                x: times,
                y: heading,
                mode: 'lines',
                name: 'Yaw (Z-Rotasyon)',
                yaxis: 'y1',
                line: { color: '#a855f7', width: 3 }
            },
            {
                x: times,
                y: rpy_r,
                mode: 'lines',
                name: 'Roll (X-Rotasyon)',
                yaxis: 'y2',
                line: { color: '#f97316', width: 1.5, dash: 'dot' }
            },
            {
                x: times,
                y: rpy_p,
                mode: 'lines',
                name: 'Pitch (Y-Rotasyon)',
                yaxis: 'y2',
                line: { color: '#3b82f6', width: 1.5, dash: 'dash' }
            }
        ];

        const rpyLayout = {
            title: 'Roll, Pitch ve Yaw Yönelim Açıları',
            xaxis: { title: 'Zaman (s)' },
            yaxis: {
                title: 'Yaw (deg)',
                titlefont: { color: '#a855f7' },
                tickfont: { color: '#a855f7' },
                range: [-180, 180]
            },
            yaxis2: {
                title: 'Roll/Pitch (deg)',
                overlaying: 'y',
                side: 'right',
                titlefont: { color: '#f97316' },
                tickfont: { color: '#f97316' },
                range: [-40, 40]
            },
            hovermode: 'x unified',
            margin: { t: 40, r: 40, b: 30, l: 40 },
            shapes: [time_marker_shape]
        };
        Plotly.newPlot('rpyPlot', rpyTraces, rpyLayout, { responsive: true, displayModeBar: false });
    } catch (e) {
        console.error("RPY Grafiği Çizim Hatası:", e);
    }

    // --- 4. Kuvvet ve Tork Grafiği ---
    try {
        const forceTorqueTraces = [
            { x: times, y: force_x, mode: 'lines', name: 'Kuvvet X (N)', yaxis: 'y1', line: { color: '#ef4444', width: 2 } },
            { x: times, y: force_y, mode: 'lines', name: 'Kuvvet Y (N)', yaxis: 'y1', line: { color: '#f97316', width: 2, dash: 'dash' } },
            { x: times, y: force_z, mode: 'lines', name: 'Kuvvet Z (N)', yaxis: 'y1', line: { color: '#0f172a', width: 1, dash: 'dot' } },
            { x: times, y: torque_z, mode: 'lines', name: 'Tork Z (Nm - Yaw)', yaxis: 'y2', line: { color: '#10b981', width: 3 } },
            { x: times, y: torque_x, mode: 'lines', name: 'Tork X (Nm - Roll)', yaxis: 'y2', line: { color: '#eab308', width: 1, dash: 'dashdot' } },
            { x: times, y: torque_y, mode: 'lines', name: 'Tork Y (Nm - Pitch)', yaxis: 'y2', line: { color: '#3b82f6', width: 1, dash: 'dashdot' } }
        ];

        const forcesAbs = force_x.map(v => Math.abs(v))
            .concat(force_y.map(v => Math.abs(v)), force_z.map(v => Math.abs(v)));
        const torquesAbs = torque_z.map(v => Math.abs(v))
            .concat(torque_x.map(v => Math.abs(v)), torque_y.map(v => Math.abs(v)));

        const maxForceCalc = safeMax(forcesAbs);
        const maxTorqueCalc = safeMax(torquesAbs);

        const maxForce = Math.max(1.2, maxForceCalc * 1.1);
        const maxTorque = Math.max(1.2, maxTorqueCalc * 1.1);

        const forceTorqueLayout = {
            title: 'Gövde Üzerindeki Toplam Kuvvet ve Tork Vektörleri',
            xaxis: { title: 'Zaman (s)' },
            yaxis: {
                title: 'Kuvvet (N)',
                titlefont: { color: '#dc2626' },
                tickfont: { color: '#dc2626' },
                range: [-maxForce, maxForce]
            },
            yaxis2: {
                title: 'Tork (Nm)',
                overlaying: 'y',
                side: 'right',
                titlefont: { color: '#10b981' },
                tickfont: { color: '#10b981' },
                range: [-maxTorque, maxTorque]
            },
            hovermode: 'x unified',
            margin: { t: 40, r: 40, b: 30, l: 40 },
            shapes: [time_marker_shape]
        };
        Plotly.newPlot('forceTorquePlot', forceTorqueTraces, forceTorqueLayout, { responsive: true, displayModeBar: false });
    } catch (e) {
        console.error("Kuvvet/Tork Grafiği Çizim Hatası:", e);
    }

    // --- 5. İtici Yerleşimi Grafiği ---
    try {
        if (Array.isArray(thrusterLayout) && thrusterLayout.length > 0) {
            const initialThrusterData = getThrusterTraces(thrusterLayout, (logData[0] && logData[0].actuators) || []);

            Plotly.newPlot('thrusterLayoutPlot', initialThrusterData.traces, {
                title: 'İtici Geometrisi ve Anlık Çıkış Kuvveti (Gövde Frame)',
                xaxis: { range: [-1.2, 1.2], title: 'Gövde X Ekseni (m)' },
                yaxis: { range: [-1.2, 1.2], title: 'Gövde Y Ekseni (m)', scaleanchor: "x", scaleratio: 1 },
                annotations: initialThrusterData.annotations,
                showlegend: false,
                margin: { t: 40, r: 20, b: 30, l: 40 }
            }, { responsive: true, displayModeBar: false });
        } else {
            // Hiç layout yoksa fallback
            updateThrusterLayoutVisualization([]);
        }
    } catch (e) {
        console.error("İtici Grafiği Çizim Hatası:", e);
        updateThrusterLayoutVisualization([]);
    }

    // Sensör & Health grafikleri
    drawSensorAndHealthPlots();
}

// --------------------------------------------------------------------
// CANLI MOD – HYDRONOM’DAN GERÇEK ZAMANLI OKUMA ALTYAPISI
// --------------------------------------------------------------------

// Not: Backend endpointleri:
//  - POST /live/start  → canlı tail thread başlat
//  - POST /live/stop   → canlı tail thread durdur
//  - GET  /live/snapshot?limit=... → son N frame + thruster info

function startLiveMode() {
    if (liveMode) return; // Zaten açıksa bir şey yapma

    // Önce backend'de canlı modu başlat
    $.ajax({
        url: '/live/start',
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ mode: 'file' }),
        success: function (resp) {
            if (!resp || !resp.success) {
                const err = (resp && resp.error) ? resp.error : 'Bilinmeyen hata';
                alert("Canlı mod başlatılamadı: " + err);
                $('#logConsole').text("Canlı mod başlatma hatası: " + err);
                setLiveStatusBadge(false);
                return;
            }

            // Backend OK ise local state'i aç
            liveMode = true;
            setLiveStatusBadge(true);

            // Eski timer'ı temizle
            if (liveTimerId !== null) {
                clearInterval(liveTimerId);
                liveTimerId = null;
            }

            // Yeni canlı oturum için veriyi sıfırla
            logData = [];
            thrusterLayout = [];
            startTimestamp = 0;
            lastHealthSnapshot = null;

            $('#controlsSection').show();
            $('#plotsContainer').show();
            $('#sensorSection').show();
            $('#logConsole').text("Canlı mod başlatıldı. Hydronom runtime verisi okunuyor...");

            // İlk poll hemen
            pollLiveSnapshot();
            // Sonra periyodik olarak
            liveTimerId = setInterval(pollLiveSnapshot, 500); // 500 ms → 2 Hz
        },
        error: function (xhr, status, error) {
            console.error("Canlı mod başlatma AJAX hatası:", error, xhr.responseText);
            alert("Canlı mod başlatılırken bir hata oluştu. Detaylar için konsola bakın.");
            $('#logConsole').text(`Canlı mod başlatma hatası: ${xhr.status} ${xhr.statusText}.`);
            setLiveStatusBadge(false);
        }
    });
}

function stopLiveMode(callBackend = true) {
    liveMode = false;

    if (liveTimerId !== null) {
        clearInterval(liveTimerId);
        liveTimerId = null;
    }

    setLiveStatusBadge(false);

    if (callBackend) {
        $.ajax({
            url: '/live/stop',
            type: 'POST',
            complete: function () {
                // Başarılı olsun olmasın, frontend tarafında durduk
                $('#logConsole').text("Canlı mod durduruldu. Son alınan veriler ekranda bırakıldı.");
            }
        });
    } else {
        $('#logConsole').text("Canlı mod durduruldu. Son alınan veriler ekranda bırakıldı.");
    }
}

// Backend’ten tek snapshot çek
function pollLiveSnapshot() {
    if (!liveMode) return;

    $.ajax({
        url: '/live/snapshot',
        type: 'GET',
        dataType: 'json',
        success: function (response) {
            handleLiveData(response);
        },
        error: function (xhr, status, error) {
            console.error("Canlı mod AJAX hatası:", error, xhr.responseText);
            $('#logConsole').text(`Canlı mod hatası: ${xhr.status} ${xhr.statusText}.`);
            // Hata durumunda canlı modu kapat ki log spam olmasın
            stopLiveMode(false);
        }
    });
}

// Canlı snapshot’ı mevcut logData’ya entegre et ve görselleştir
function handleLiveData(response) {
    if (!response || !response.success) {
        // success false ise çok gürültü yapma, sadece logla
        if (response && response.error) {
            console.warn("Live snapshot error:", response.error);
        }
        setLiveStatusBadge(response && response.running);
        if (response && response.running === false) {
            // Backend durduysa canlı modu da kapat
            stopLiveMode(false);
        }
        return;
    }

    // Backend'ten gelen data: /live/snapshot JSON'u
    // {
    //   success: true,
    //   running: true/false,
    //   data: [ { ...state fields... }, ... ],
    //   thruster_count: int,
    //   thruster_layout: [ ... ]
    // }
    setLiveStatusBadge(response.running);

    // Gelen veri: dizi
    let incoming = [];
    if (Array.isArray(response.data) && response.data.length > 0) {
        incoming = response.data;
    }

    if (incoming.length === 0) {
        // Henüz kayıt yoksa sadece badge güncellemesi yeterli
        if (response.running === false) {
            stopLiveMode(false);
        }
        return;
    }

    // Type normalizasyonu
    const converted = incoming.map(convertDataPoint);

    // logData'yı sliding-window olarak backend'den gelenle eşitle
    logData = converted;

    // Başlangıç timestamp
    if (logData[0] && logData[0].time) {
        startTimestamp = new Date(logData[0].time).getTime() / 1000;
    }

    // Hedef varsa çek
    const firstDataPoint = logData.find(d =>
        d.target_x !== null &&
        d.target_y !== null &&
        !isNaN(d.target_x) &&
        !isNaN(d.target_y)
    );
    if (firstDataPoint) {
        targetPoint = {
            x: firstDataPoint.target_x,
            y: firstDataPoint.target_y
        };
    }

    // Thruster info backend’ten geliyorsa al
    let thrusterCount = 0;
    if (typeof response.thruster_count === 'number' && response.thruster_count > 0) {
        thrusterCount = response.thruster_count;
    }

    if (Array.isArray(response.thruster_layout) && response.thruster_layout.length > 0) {
        thrusterLayout = response.thruster_layout;
    }

    // ThrusterCount hala 0 ise actuator uzunluğundan tahmin
    if (!thrusterCount || thrusterCount === 0) {
        if (Array.isArray(thrusterLayout) && thrusterLayout.length > 0) {
            thrusterCount = thrusterLayout.length;
        } else {
            let maxActLen = 0;
            for (const dp of logData) {
                const arr = Array.isArray(dp.actuators) ? dp.actuators : [];
                if (arr.length > maxActLen) maxActLen = arr.length;
            }
            thrusterCount = maxActLen;
        }
    }

    // Layout yoksa sentetik üret
    if ((!thrusterLayout || thrusterLayout.length === 0) && thrusterCount > 0) {
        thrusterLayout = buildSyntheticThrusterLayout(thrusterCount);
    }

    if ((!thrusterCount || thrusterCount === 0) && Array.isArray(thrusterLayout)) {
        thrusterCount = thrusterLayout.length;
    }

    // Kontrol ve grafik kurulumu (her poll’de komple yeniliyoruz, 2 Hz için yeterli)
    initializeControls(thrusterCount);
    drawPlots();

    // Slider’ı son adıma getir ve o adımı göster
    const lastIndex = logData.length - 1;
    const slider = document.getElementById('timeSlider');
    slider.max = Math.max(lastIndex, 0);
    slider.value = lastIndex;
    updateVisualization(lastIndex);

    // Backend running=false ise son snapshot alındı, canlı modu kapatalım
    if (response.running === false) {
        stopLiveMode(false);
    }
}

// --------------------------------------------------------------------
// NOT:
// HTML tarafında butonlar:
//   <button type="button" onclick="startLiveMode()">▶ Canlı Modu Başlat</button>
//   <button type="button" onclick="stopLiveMode()">⏹ Durdur</button>
// doğrudan bu fonksiyonları kullanıyor.
// --------------------------------------------------------------------
