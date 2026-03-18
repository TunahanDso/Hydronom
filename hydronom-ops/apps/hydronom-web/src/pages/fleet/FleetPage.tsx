export function FleetPage() {
  return (
    <PageFrame
      title="Fleet"
      description="Çoklu araç desteği, araç listesi ve genel filo görünümü burada şekillenecek."
    />
  );
}

function PageFrame(props: { title: string; description: string }) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6 shadow-panel">
      <h1 className="text-3xl font-bold tracking-tight">{props.title}</h1>
      <p className="mt-2 max-w-3xl text-sm text-slate-400">{props.description}</p>
      <div className="mt-6 flex h-[620px] items-center justify-center rounded-2xl border border-dashed border-slate-700 bg-slate-950/50 text-sm text-slate-500">
        Fleet görünümü burada yer alacak
      </div>
    </section>
  );
}