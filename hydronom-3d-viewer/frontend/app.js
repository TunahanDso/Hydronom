// frontend/app.js

let scene, camera, renderer, controls;
let boat;
let trailPoints = [];
let trailLine;

// Hedef (goal) için 3D bayrak
let targetMesh = null;
let currentTargetLocal = null;

// LiDAR için 3D grup
let lidarGroup = null;
let currentLidarScan = null;

// CSM / Aktüatör görselleri
let forceArrow; // CSM Kuvvet Vektörü (Fb)
let thrusters = {}; // Pervane meshleri

const deg2rad = (deg) => (deg * Math.PI) / 180.0;

function setStatus(text, color) {
    const statusDot = document.getElementById("status-dot");
    const statusText = document.getElementById("status-text");
    statusDot.style.background = color;
    statusText.textContent = text;
}

function setDebug(text) {
    const debugText = document.getElementById("debug-text");
    if (debugText) {
        debugText.textContent = text;
    }
}

// Origin mantığı (ilk gerçek frame'i 0,0,0 kabul ediyoruz)
let originSet = false;
let origin = { x: 0, y: 0, z: 0 };

function toLocalPos(pos, stamp) {
    // Dummy frame'de stamp null, gerçek loglarda timestamp var.
    if (!originSet && stamp !== null) {
        origin = { x: pos.x, y: pos.y, z: pos.z };
        originSet = true;
        setDebug(
            `Origin belirlendi: (${origin.x.toFixed(2)}, ${origin.y.toFixed(
                2
            )}, ${origin.z.toFixed(2)})`
        );
    }

    const ox = originSet ? origin.x : 0;
    const oy = originSet ? origin.y : 0;
    const oz = originSet ? origin.z : 0;

    return {
        x: pos.x - ox,
        y: pos.y - oy,
        z: pos.z - oz,
    };
}

/* ===================== TEKNE MODELİ ===================== */

function createBoatModel() {
    const group = new THREE.Group();

    // Ana Gövde
    const boatGeom = new THREE.BoxGeometry(4, 0.6, 1.6);
    const boatMat = new THREE.MeshStandardMaterial({
        color: 0x38bdf8,
        metalness: 0.5,
        roughness: 0.2,
        emissive: 0x000000,
        emissiveIntensity: 0.0,
    });
    const hull = new THREE.Mesh(boatGeom, boatMat);
    group.add(hull);

    // Pervaneler
    const propGeom = new THREE.CylinderGeometry(0.3, 0.3, 0.1, 8);
    const propMat = new THREE.MeshStandardMaterial({ color: 0xfacc15 });

    const positions = {
        FL: [1.8, -0.3, 0.8],
        FR: [1.8, -0.3, -0.8],
        RL: [-1.8, -0.3, 0.8],
        RR: [-1.8, -0.3, -0.8],
    };

    thrusters = {};

    for (const [id, pos] of Object.entries(positions)) {
        const prop = new THREE.Mesh(propGeom, propMat);
        prop.position.set(...pos);
        prop.rotation.x = Math.PI / 2;
        thrusters[id] = prop;
        group.add(prop);
    }

    // Kuvvet vektörü
    forceArrow = new THREE.ArrowHelper(
        new THREE.Vector3(1, 0, 0),
        new THREE.Vector3(0, 0, 0),
        0,
        0xff0000
    );
    forceArrow.visible = false;
    group.add(forceArrow);

    return group;
}

/* ===================== 3D SAHNE ===================== */

function initScene() {
    try {
        const container = document.getElementById("canvas-container");

        scene = new THREE.Scene();
        scene.background = new THREE.Color(0x020617);

        const aspect = window.innerWidth / window.innerHeight;
        camera = new THREE.PerspectiveCamera(60, aspect, 0.1, 1000);
        // Kamerayı karşı tarafa al
        camera.position.set(0, 20, -30);
        camera.lookAt(0, 0, 0);

        renderer = new THREE.WebGLRenderer({ antialias: true });
        renderer.setSize(window.innerWidth, window.innerHeight);
        container.appendChild(renderer.domElement);

        // OrbitControls
        try {
            if (THREE.OrbitControls) {
                controls = new THREE.OrbitControls(camera, renderer.domElement);
                controls.target.set(0, 0, 0);
                controls.update();
            }
        } catch (e) {
            console.warn("OrbitControls kullanılamadı:", e);
            controls = null;
            setDebug("OrbitControls yok, sabit kamera ile devam");
        }

        window.addEventListener("resize", onWindowResize);

        // Işıklar
        const ambient = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambient);

        const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
        dirLight.position.set(10, 20, 10);
        scene.add(dirLight);

        // Grid
        const grid = new THREE.GridHelper(400, 80, 0x334155, 0x1f2937);
        scene.add(grid);

        // Eksenler
        const axes = new THREE.AxesHelper(5);
        scene.add(axes);

        // Tekne modeli
        boat = createBoatModel();
        boat.position.set(0, 0, 0);
        scene.add(boat);

        // Rota izi
        const trailGeom = new THREE.BufferGeometry().setFromPoints([]);
        const trailMat = new THREE.LineBasicMaterial();
        trailLine = new THREE.Line(trailGeom, trailMat);
        scene.add(trailLine);

        // Hedef bayrağı
        const flagGeom = new THREE.ConeGeometry(0.8, 3, 16);
        const flagMat = new THREE.MeshStandardMaterial({ color: 0xf97316 });
        targetMesh = new THREE.Mesh(flagGeom, flagMat);
        targetMesh.position.set(0, 0, 0);
        targetMesh.visible = false;
        scene.add(targetMesh);

        // LiDAR noktaları için grup
        lidarGroup = new THREE.Group();
        scene.add(lidarGroup);

        animate();
    } catch (err) {
        console.error("initScene error:", err);
        setDebug("initScene hata: " + err.message);
    }
}

function onWindowResize() {
    if (!camera || !renderer) return;
    const width = window.innerWidth;
    const height = window.innerHeight;
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    renderer.setSize(width, height);
}

function animate() {
    requestAnimationFrame(animate);
    if (controls) {
        controls.update();
    }
    if (renderer && scene && camera) {
        renderer.render(scene, camera);
    }
}

/* ===================== 3D LiDAR ÇİZİMİ ===================== */

function updateLidar3D(localPos, yawDeg, lidar) {
    if (!lidarGroup || !lidar || !Array.isArray(lidar.ranges)) return;

    // Eski noktaları temizle
    while (lidarGroup.children.length > 0) {
        const child = lidarGroup.children.pop();
        if (child) {
            if (child.geometry && child.geometry.dispose) child.geometry.dispose();
            if (child.material && child.material.dispose) child.material.dispose();
            lidarGroup.remove(child);
        }
    }

    const pts = [];
    const angleMin = lidar.angle_min;
    const angleInc = lidar.angle_increment;
    const rMin = lidar.range_min;
    const rMax = lidar.range_max;
    const ranges = lidar.ranges;

    const yawRad = deg2rad(yawDeg);

    for (let i = 0; i < ranges.length; i++) {
        const r = ranges[i];
        if (typeof r !== "number" || !isFinite(r)) continue;
        if (r < rMin || r > rMax) continue;

        const angle = angleMin + i * angleInc;

        // Tekne gövde koordinatları
        const bx = Math.cos(angle) * r;
        const by = Math.sin(angle) * r;

        // Gövde -> dünya
        const wx2d =
            localPos.x + (bx * Math.cos(yawRad) - by * Math.sin(yawRad));
        const wy2d =
            localPos.y + (bx * Math.sin(yawRad) + by * Math.cos(yawRad));

        // Three.js: XZ düzlemi, Y yukarı
        const px = wx2d;
        const pz = -wy2d;
        const py = 0.05;

        pts.push(new THREE.Vector3(px, py, pz));
    }

    if (pts.length === 0) return;

    const geom = new THREE.BufferGeometry().setFromPoints(pts);
    const mat = new THREE.PointsMaterial({ size: 0.3 });
    const cloud = new THREE.Points(geom, mat);
    lidarGroup.add(cloud);
}

/* ===================== MINI XY MAP ===================== */

let mapCanvas = null;
let mapCtx = null;
const MAP_SIZE = 220;
const MAP_SCALE = 1.5; // px / metre
let trail2D = [];

function initMiniMap() {
    mapCanvas = document.getElementById("xy-map");
    if (!mapCanvas) {
        setDebug("XY map canvas bulunamadı");
        return;
    }
    mapCanvas.width = MAP_SIZE;
    mapCanvas.height = MAP_SIZE;
    mapCtx = mapCanvas.getContext("2d");
}

function updateMiniMap(localPos, yawDeg, targetLocal, lidar) {
    if (!mapCtx) return;

    const cx = MAP_SIZE / 2;
    const cy = MAP_SIZE / 2;

    // Arka plan
    mapCtx.fillStyle = "rgba(15,23,42,0.95)";
    mapCtx.fillRect(0, 0, MAP_SIZE, MAP_SIZE);

    // Grid çizgileri
    mapCtx.strokeStyle = "rgba(148,163,184,0.25)";
    mapCtx.lineWidth = 1;
    const step = 20;
    for (let i = 0; i <= MAP_SIZE; i += step) {
        mapCtx.beginPath();
        mapCtx.moveTo(i, 0);
        mapCtx.lineTo(i, MAP_SIZE);
        mapCtx.stroke();

        mapCtx.beginPath();
        mapCtx.moveTo(0, i);
        mapCtx.lineTo(MAP_SIZE, i);
        mapCtx.stroke();
    }

    // Eksenler
    mapCtx.strokeStyle = "rgba(248,250,252,0.6)";
    mapCtx.beginPath();
    mapCtx.moveTo(cx, 0);
    mapCtx.lineTo(cx, MAP_SIZE);
    mapCtx.stroke();

    mapCtx.beginPath();
    mapCtx.moveTo(0, cy);
    mapCtx.lineTo(MAP_SIZE, cy);
    mapCtx.stroke();

    // Trail
    trail2D.push({ x: localPos.x, y: localPos.y });
    if (trail2D.length > 2000) {
        trail2D.shift();
    }

    const bxWorld = localPos.x;
    const byWorld = localPos.y;

    mapCtx.strokeStyle = "rgba(56,189,248,0.9)";
    mapCtx.lineWidth = 2;
    mapCtx.beginPath();
    for (let i = 0; i < trail2D.length; i++) {
        const p = trail2D[i];
        const dx = p.x - bxWorld;
        const dy = p.y - byWorld;

        const sx = cx + dx * MAP_SCALE;
        const sy = cy - dy * MAP_SCALE;

        if (i === 0) {
            mapCtx.moveTo(sx, sy);
        } else {
            mapCtx.lineTo(sx, sy);
        }
    }
    mapCtx.stroke();

    // LiDAR noktaları
    if (lidar && Array.isArray(lidar.ranges)) {
        const angleMin = lidar.angle_min;
        const angleInc = lidar.angle_increment;
        const rMin = lidar.range_min;
        const rMax = lidar.range_max;
        const ranges = lidar.ranges;
        const yawRad = deg2rad(yawDeg);

        mapCtx.fillStyle = "rgba(74,222,128,0.9)";

        for (let i = 0; i < ranges.length; i++) {
            const r = ranges[i];
            if (typeof r !== "number" || !isFinite(r)) continue;
            if (r < rMin || r > rMax) continue;

            const angle = angleMin + i * angleInc;

            const bx = Math.cos(angle) * r;
            const by = Math.sin(angle) * r;

            const wx =
                localPos.x +
                (bx * Math.cos(yawRad) - by * Math.sin(yawRad));
            const wy =
                localPos.y +
                (bx * Math.sin(yawRad) + by * Math.cos(yawRad));

            const dx = wx - bxWorld;
            const dy = wy - byWorld;

            const sx = cx + dx * MAP_SCALE;
            const sy = cy - dy * MAP_SCALE;

            mapCtx.fillRect(sx - 1, sy - 1, 2, 2);
        }
    }

    // Tekne üçgeni
    const bx = cx;
    const by = cy;

    const yawRad = deg2rad(yawDeg);
    const size = 6;

    const nose = {
        x: bx + Math.cos(yawRad) * size * 2,
        y: by - Math.sin(yawRad) * size * 2,
    };
    const left = {
        x: bx + Math.cos(yawRad + Math.PI * 0.75) * size,
        y: by - Math.sin(yawRad + Math.PI * 0.75) * size,
    };
    const right = {
        x: bx + Math.cos(yawRad - Math.PI * 0.75) * size,
        y: by - Math.sin(yawRad - Math.PI * 0.75) * size,
    };

    mapCtx.fillStyle = "#f97316";
    mapCtx.beginPath();
    mapCtx.moveTo(nose.x, nose.y);
    mapCtx.lineTo(left.x, left.y);
    mapCtx.lineTo(right.x, right.y);
    mapCtx.closePath();
    mapCtx.fill();

    // Hedef
    if (targetLocal) {
        const dx = targetLocal.x - bxWorld;
        const dy = targetLocal.y - byWorld;

        const tx = cx + dx * MAP_SCALE;
        const ty = cy - dy * MAP_SCALE;

        mapCtx.fillStyle = "#e11d48";
        mapCtx.beginPath();
        mapCtx.arc(tx, ty, 5, 0, 2 * Math.PI);
        mapCtx.fill();

        mapCtx.strokeStyle = "#e11d48";
        mapCtx.beginPath();
        mapCtx.moveTo(tx, ty);
        mapCtx.lineTo(tx, ty - 10);
        mapCtx.stroke();
    }
}

/* ===================== WEBSOCKET ===================== */

function connectWebSocket() {
    const posText = document.getElementById("pos-text");
    const rpyText = document.getElementById("rpy-text");

    const host = window.location.hostname || "localhost";
    const url = `ws://${host}:8000/ws/live`;

    setDebug("WS URL: " + url);

    let ws;
    try {
        ws = new WebSocket(url);
    } catch (err) {
        console.error("WebSocket ctor error:", err);
        setStatus("Bağlantı oluşturulamadı (ctor)", "#ef4444");
        setDebug("WebSocket ctor hata: " + err.message);
        return;
    }

    ws.onopen = () => {
        setStatus("Canlı bağlantı: açık", "#22c55e");
        setDebug("WebSocket open");
        console.log("[ws] connected");
    };

    ws.onclose = () => {
        setStatus("Bağlantı kapandı", "#ef4444");
        setDebug("WebSocket close");
        console.log("[ws] closed");
    };

    ws.onerror = (err) => {
        setStatus("Bağlantı hatası", "#ef4444");
        setDebug("WebSocket error (detay için konsola bak)");
        console.error("[ws] error", err);
    };

    ws.onmessage = (event) => {
        try {
            const data = JSON.parse(event.data);
            // Backend'den beklenen örnek alanlar:
            // { pos, rpy, stamp, target?, lidar?, forces?, actuators?, is_armed? }
            const { pos, rpy, stamp, target, lidar, forces, actuators, is_armed } = data;

            if (!boat) {
                setDebug("Uyarı: boat henüz hazır değil, frame atlandı");
                return;
            }

            posText.textContent = `pos: ${pos.x.toFixed(2)}, ${pos.y.toFixed(
                2
            )}, ${pos.z.toFixed(2)}`;
            rpyText.textContent = `rpy: ${rpy.roll.toFixed(
                1
            )}, ${rpy.pitch.toFixed(1)}, ${rpy.yaw.toFixed(1)}`;

            // Pozisyonu origin'e göre merkezle
            const local = toLocalPos(pos, stamp);

            // World-plane
            const worldX = local.x;
            const worldY = -local.y;

            // 3D sahnede XZ düzlemi
            const boatX = worldX;
            const boatZ = worldY;
            const boatY = 0;

            boat.position.set(boatX, boatY, boatZ);

            boat.rotation.set(
                deg2rad(rpy.roll),
                deg2rad(rpy.yaw),
                deg2rad(rpy.pitch)
            );

            // 3D rota izi
            trailPoints.push(new THREE.Vector3(boatX, boatY + 0.01, boatZ));
            if (trailPoints.length > 2000) {
                trailPoints.shift();
            }
            trailLine.geometry.setFromPoints(trailPoints);

            // Hedef
            if (target && typeof target.x === "number" && typeof target.y === "number") {
                currentTargetLocal = toLocalPos(
                    { x: target.x, y: target.y, z: target.z || 0 },
                    null
                );

                if (targetMesh) {
                    const tx2d = currentTargetLocal.x;
                    const ty2d = currentTargetLocal.y;
                    const tx3d = tx2d;
                    const tz3d = -ty2d;
                    const ty3d = 0;

                    targetMesh.position.set(tx3d, ty3d + 1.5, tz3d);
                    targetMesh.visible = true;
                }
            } else {
                currentTargetLocal = null;
                if (targetMesh) {
                    targetMesh.visible = false;
                }
            }

            // LiDAR
            if (lidar && Array.isArray(lidar.ranges)) {
                currentLidarScan = lidar;
                updateLidar3D(local, rpy.yaw, lidar);
            } else {
                currentLidarScan = null;
            }

            // Aktüatör animasyonu
            if (actuators) {
                for (const [id, status] of Object.entries(actuators)) {
                    if (thrusters[id] && status) {
                        const rpm = typeof status.rpm === "number" ? status.rpm : 0;
                        const speed = rpm / 1000;
                        thrusters[id].rotation.y += speed;
                    }
                }
            }

            // Kuvvet vektörü
            if (forces && forces.fb && Array.isArray(forces.fb) && forces.fb.length >= 3) {
                const [fx, fy, fz] = forces.fb;
                const forceVec = new THREE.Vector3(fx, -fz, fy);
                const length = forceVec.length();

                if (forceArrow) {
                    if (length > 0.1) {
                        forceArrow.setDirection(forceVec.clone().normalize());
                        forceArrow.setLength(length * 0.5);
                        forceArrow.visible = true;
                    } else {
                        forceArrow.visible = false;
                    }
                }
            } else if (forceArrow) {
                forceArrow.visible = false;
            }

            // Armed / disarmed gövde rengi
            if (is_armed !== undefined && boat && boat.children && boat.children[0]) {
                const boatHull = boat.children[0];
                if (boatHull.material && boatHull.material.emissive) {
                    boatHull.material.emissive.setHex(is_armed ? 0x22c55e : 0xef4444);
                    boatHull.material.emissiveIntensity = 0.3;
                }
            }

            // Kamera takip
            const followOffset = new THREE.Vector3(0, 10, 20);
            const desiredCameraPos = new THREE.Vector3(
                boatX,
                boatY,
                boatZ
            ).add(followOffset);

            if (camera) {
                camera.position.lerp(desiredCameraPos, 0.1);

                if (controls && controls.target) {
                    controls.target.lerp(boat.position, 0.1);
                    controls.update();
                } else {
                    camera.lookAt(boat.position);
                }
            }

            // Mini map güncelle
            updateMiniMap(local, rpy.yaw, currentTargetLocal, currentLidarScan);
        } catch (err) {
            console.error("onmessage error:", err);
            setDebug("onmessage hata: " + err.message);
        }
    };
}

/* ===================== BOOTSTRAP ===================== */

window.addEventListener("load", () => {
    setStatus("Bağlanıyor...", "#eab308");
    initScene();
    initMiniMap();
    connectWebSocket();
});