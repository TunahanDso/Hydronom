# app.py
from flask import Flask, request, jsonify, render_template
import io
import os

# ---------------------------------------------------------------------------
# MODÜL İMPORTLARI
# ---------------------------------------------------------------------------
# Not:
# - Eğer proje yapın:
#     HydroScan/
#       app.py
#       parsing.py
#       live.py
#   şeklindeyse, doğrudan 'parsing' ve 'live' importları çalışır.
#
# - Eğer:
#     HydroScan/
#       hydroscan/
#         __init__.py
#         parsing.py
#         live.py
#       app.py
#   gibi paket yapısı kullanırsan, hydroscan.parsing/live importları devreye girer.
try:
    # Paketli kullanım (HydroScan/hydroscan/...)
    from hydroscan.parsing import parse_log_content, extract_thruster_info
    from hydroscan.live import (
        start_live_file,
        stop_live,
        is_live_running,
        get_live_snapshot,
        get_live_thruster_info,
    )
except ImportError:
    # Düz klasör kullanım (HydroScan/app.py + parsing.py + live.py)
    from parsing import parse_log_content, extract_thruster_info
    from live import (
        start_live_file,
        stop_live,
        is_live_running,
        get_live_snapshot,
        get_live_thruster_info,
    )

app = Flask(__name__)


# ---------------------------------------------------------------------------
# ANA SAYFA
# ---------------------------------------------------------------------------

@app.route("/")
def index():
    """
    HydroScan ana arayüzü.

    Not:
    - HTML dosyasını 'templates/index.html' olarak kaydettiğimizi varsayıyoruz.
      Eğer istersen isim değiştirip burada da güncelleyebilirsin.
    """
    return render_template("index.html")


# ---------------------------------------------------------------------------
# OFFLINE MOD: LOG DOSYASI YÜKLEME VE ANALİZ
# ---------------------------------------------------------------------------

@app.route("/upload-log", methods=["POST"])
def upload_log():
    """
    Kullanıcının yüklediği .txt veya .docx log dosyasını parse edip JSON döndürür.

    - Log içeriği parse_log_content() ile satır satır ayrıştırılır.
    - Ardından extract_thruster_info() ile itici sayısı ve geometri tahmin edilir.
    - Frontend tarafı, bu JSON'u direkt çizim ve dashboard için kullanır.
    """
    if "logFile" not in request.files:
        return jsonify({"success": False, "error": "Dosya yüklenmedi."}), 400

    file = request.files["logFile"]
    if file.filename == "":
        return jsonify({"success": False, "error": "Dosya adı boş."}), 400

    log_content = ""

    filename_lower = file.filename.lower()

    # ----------------------------------------------------------------------
    # Word (.docx) dosyasını okuma
    # ----------------------------------------------------------------------
    if filename_lower.endswith(".docx"):
        try:
            # python-docx importunu yalnızca gerektiğinde yap
            from docx import Document  # type: ignore
        except ImportError:
            # python-docx yüklü değilse kullanıcıya kibar bir hata mesajı ver
            return (
                jsonify(
                    {
                        "success": False,
                        "error": (
                            "DOCX okuma hatası: 'python-docx' paketi kurulu değil. "
                            "Lütfen 'pip install python-docx' komutunu çalıştırın "
                            "veya log dosyasını .txt formatında yükleyin."
                        ),
                    }
                ),
                500,
            )

        try:
            # Word içeriğini bellek üzerinde oku
            doc = Document(io.BytesIO(file.read()))
            for para in doc.paragraphs:
                log_content += para.text + "\n"
        except Exception as e:
            return (
                jsonify(
                    {
                        "success": False,
                        "error": f"Word dosyası işlenirken hata: {e}",
                    }
                ),
                500,
            )

    # ----------------------------------------------------------------------
    # Düz metin (.txt) dosyasını okuma
    # ----------------------------------------------------------------------
    elif filename_lower.endswith(".txt"):
        try:
            # UTF-8 BOM olasılığına karşı utf-8-sig kullanmak daha güvenli
            log_content = file.read().decode("utf-8-sig")
        except Exception as e:
            return (
                jsonify(
                    {
                        "success": False,
                        "error": f"TXT dosyası okuma/kodlama hatası: {e}",
                    }
                ),
                500,
            )

    else:
        # Desteklenmeyen uzantı
        return (
            jsonify(
                {
                    "success": False,
                    "error": "Desteklenmeyen dosya formatı. Lütfen .docx veya .txt yükleyin.",
                }
            ),
            400,
        )

    # ----------------------------------------------------------------------
    # Log içeriğini ayrıştır (STATE satırlarını JSON formatına çevir)
    # ----------------------------------------------------------------------
    parsed_data = parse_log_content(log_content)

    if not parsed_data:
        # STATE yoksa zaten yörünge ve 6DoF analiz yapamayız, direkt anlamlı hata döndür.
        return (
            jsonify(
                {
                    "success": False,
                    "error": (
                        "Log dosyasında analiz edilecek geçerli STATE verisi bulunamadı. "
                        "(Kontrol: pos= ve vel= içeren satırlar)"
                    ),
                }
            ),
            400,
        )

    # ----------------------------------------------------------------------
    # İtici sayısı ve geometriyi çıkar
    #   - thruster_count: tahmini/çıkarılan toplam itici sayısı
    #   - thruster_layout: her itici için {name, channel, pos_x, pos_y, ...}
    # ----------------------------------------------------------------------
    thruster_count, thruster_layout = extract_thruster_info(log_content, parsed_data)

    return jsonify(
        {
            "success": True,
            "data": parsed_data,
            "thruster_count": thruster_count,
            "thruster_layout": thruster_layout,
        }
    )


# ---------------------------------------------------------------------------
# CANLI MOD: LOG DOSYASINI TAIL EDEREK ANLIK VERİ OKUMA
# ---------------------------------------------------------------------------

@app.route("/live/start", methods=["POST"])
def live_start():
    """
    Canlı modu başlatır.

    Basit versiyon:
      body: { "mode": "file", "path": "C:/.../hydronom_runtime.log" }

    - Şimdilik sadece 'file' modu var: arka planda bir thread açıp ilgili dosyayı
      tail eder, satırları parse edip canlı buffer'a aktarır.
    - path verilmezse varsayılan bir log dosyası ismi kullanılır.

    Önemli:
    - start_live_file(path) fonksiyonu, dosya mevcut değilse False döndürür.
      Bu durumda frontend'e "log yok / path yanlış" şeklinde net bir hata
      dönüyoruz. Yani şu anki tasarımda:
        * Önce Hydronom Runtime log dosyasını oluşturmalı,
        * Ardından HydroScan canlı modu başlatmalısın.
    """
    data = request.get_json(silent=True) or {}
    mode = data.get("mode", "file")

    if mode != "file":
        # İleride TCP soketi, UDP, ZeroMQ vb. modları da ekleyebiliriz (mode='tcp' vs.).
        return (
            jsonify(
                {
                    "success": False,
                    "error": "Şimdilik sadece 'file' modu destekleniyor.",
                }
            ),
            400,
        )

    log_path = data.get("path")

    if not log_path:
        # Varsayılan path: proje kökünde 'hydronom_runtime.log'
        # Bunu Hydronom runtime'ın gerçek log konumuna göre güncelleyebilirsin.
        log_path = os.path.join(os.getcwd(), "hydronom_runtime.log")

    # start_live_file(path), dosya yoksa False döndürüyor.
    started = start_live_file(log_path)
    if not started:
        # Dosya mevcut değilse veya thread/dosya açma hatası varsa
        exists = os.path.exists(log_path)
        if not exists:
            error_msg = (
                "Canlı mod başlatılamadı: log dosyası bulunamadı. "
                "Önce Hydronom Runtime'ın ilgili path'te log üretip üretmediğini kontrol et:\n"
                f"  {log_path}"
            )
            status_code = 400
        else:
            error_msg = (
                "Canlı mod başlatılamadı (thread veya dosya okuma hatası). "
                "Sunucu loglarını kontrol et."
            )
            status_code = 500

        return jsonify({"success": False, "error": error_msg}), status_code

    return jsonify(
        {
            "success": True,
            "mode": "file",
            "path": log_path,
        }
    )


@app.route("/live/stop", methods=["POST"])
def live_stop():
    """
    Canlı modu durdurur.

    - Arka plandaki tail thread'ini durdurur.
    - Buffer temizleme kararını live.py içinde sen nasıl tasarladıysan ona göre davranır.
    """
    stop_live()
    return jsonify({"success": True})


@app.route("/live/snapshot", methods=["GET"])
def live_snapshot():
    """
    Canlı buffer içindeki son frame'leri ve tahmini itici bilgisini döndürür.

    Frontend tarafı, offline modda dönen JSON ile aynı data formatını kullanır:
      {
        success: true,
        running: true/false,
        data: [ { ...state fields... }, ... ],
        thruster_count: int,
        thruster_layout: [ { ...thruster geometry... }, ... ]
      }

    - limit parametresi ile en fazla kaç kayıt döneceğini belirleyebilirsin.
      Örn: /live/snapshot?limit=500
    """
    limit = request.args.get("limit", default=300, type=int)
    data = get_live_snapshot(limit=limit)
    thruster_count, thruster_layout = get_live_thruster_info()

    return jsonify(
        {
            "success": True,
            "running": is_live_running(),
            "data": data,
            "thruster_count": thruster_count,
            "thruster_layout": thruster_layout,
        }
    )


# ---------------------------------------------------------------------------
# UYGULAMA GİRİŞ NOKTASI
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    # debug=True: kod değişince otomatik reload ve hataları daha net görürsün.
    # host='0.0.0.0' istersen ağına da açarsın, şimdilik localde 127.0.0.1 yeterli.
    app.run(debug=True)
