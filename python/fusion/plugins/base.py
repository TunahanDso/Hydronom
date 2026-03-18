# fusion/plugins/base.py
from __future__ import annotations

# Not: Protocol ile tür sözleşmesi veriyoruz; ayrıca kolaylık için bir temel sınıf da sunuyoruz.
from typing import Protocol, List, Optional, runtime_checkable, Any, Dict

from fusion.context import FusionContext
from core.sample import Sample
from core.fused_state import FusedState


@runtime_checkable
class IFuserPlugin(Protocol):
    """
    Fuser eklentileri için standart arayüz (sözleşme).

    Yaşam Döngüsü (lifecycle) sırası:
      on_init(ctx) → {her kare: on_samples(ctx,samples) → on_before_emit(ctx,out_state)} → on_close(ctx)

    Tasarım Notları:
    - Tüm bağlam erişimleri FusionContext üzerinden yapılır (poz, hız, landmark ekleme vb.).
    - Eklentiler deterministik olmalı; iç durumları resetlenebilir olmalı.
    - İsteğe bağlı oran kontrolü için `max_hz` ve önceliklendirme için `priority` meta alanları kullanılabilir.
    """

    # Zorunlu meta
    name: str

    # Opsiyonel meta (oran kontrolü/scheduling)
    priority: int        # küçük değer → daha yüksek öncelik (varsayılan 100)
    max_hz: Optional[float]  # None veya >0 (örn. 10.0 → saniyede en çok 10 kez çalış)

    def on_init(self, ctx: FusionContext) -> None:
        """Fuser başlarken tek seferlik hazırlık (opsiyonel)."""
        ...

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        """Her update() çağrısında yeni örnekler geldiğinde çalışır."""
        ...

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        """maybe_emit() çıktısı üretilmeden hemen önce, son dokunuş için çağrılır."""
        ...

    def on_close(self, ctx: FusionContext) -> None:
        """Fuser kapanırken temizlik (opsiyonel)."""
        ...


class FuserPluginBase:
    """
    Kolaylık sınıfı: IFuserPlugin sözleşmesini no-op varsayılanlarla uygular.
    İsteyen eklentiler bu sınıftan kalıtım alarak yalnızca gerekli kancaları override edebilir.
    """

    # Varsayılan meta
    name: str = "plugin"
    priority: int = 100
    max_hz: Optional[float] = None  # None → sınırsız, aksi halde eklenti tarafında kontrol edilebilir

    def on_init(self, ctx: FusionContext) -> None:
        # Başlatma için varsayılan olarak hiçbir şey yapma
        pass

    def on_samples(self, ctx: FusionContext, samples: List[Sample]) -> None:
        # Varsayılan olarak hiçbir şey yapma
        pass

    def on_before_emit(self, ctx: FusionContext, out_state: FusedState) -> None:
        # Varsayılan olarak hiçbir şey yapma
        pass

    def on_close(self, ctx: FusionContext) -> None:
        # Varsayılan olarak hiçbir şey yapma
        pass
