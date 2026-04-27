namespace SOPRO.WinForms.Forms.Disenador;

public class CanvasCommandService
{
    public bool SnapActivo      { get; set; } = true;
    public int  SnapIntervaloMm { get; set; } = 1;

    private ElementoCanvas? _clipboard;
    public  bool HayClipboard => _clipboard != null;

    public int Snap(int dmm) => SnapActivo ? Unidades.SnapA(dmm, SnapIntervaloMm) : dmm;

    public ElementoCanvas Agregar(List<ElementoCanvas> lista, ElementoCanvas el)
    {
        el.ZOrder = lista.Count > 0 ? lista.Max(e => e.ZOrder) + 1 : 0;
        el.X = Snap(el.X); el.Y = Snap(el.Y);
        lista.Add(el);
        return el;
    }

    public void Eliminar(List<ElementoCanvas> lista, ElementoCanvas el)
    {
        lista.Remove(el);
        NormZOrder(lista);
    }

    public ElementoCanvas? Duplicar(List<ElementoCanvas> lista, ElementoCanvas? el)
    {
        if (el == null || lista.Count == 0) return null;
        var c = el.Clonar();
        c.X += Unidades.MmADmm(5);
        c.Y += Unidades.MmADmm(5);
        c.ZOrder = lista.Max(e => e.ZOrder) + 1;
        lista.Add(c);
        return c;
    }

    public void Copiar(ElementoCanvas? el) { if (el != null) _clipboard = el.Clonar(); }

    public ElementoCanvas? Pegar(List<ElementoCanvas> lista)
    {
        if (_clipboard == null) return null;
        var n = _clipboard.Clonar();
        n.X += Unidades.MmADmm(5);
        n.Y += Unidades.MmADmm(5);
        n.ZOrder = lista.Count > 0 ? lista.Max(e => e.ZOrder) + 1 : 0;
        lista.Add(n);
        return n;
    }

    public void TraerAlFrente(List<ElementoCanvas> lista, ElementoCanvas? el)
    {
        if (el == null || lista.Count < 2) return;
        el.ZOrder = lista.Max(e => e.ZOrder) + 1;
        NormZOrder(lista);
    }

    public void EnviarAtras(List<ElementoCanvas> lista, ElementoCanvas? el)
    {
        if (el == null || lista.Count < 2) return;
        el.ZOrder = lista.Min(e => e.ZOrder) - 1;
        NormZOrder(lista);
    }

    public void MoverConTeclado(ElementoCanvas? el, Keys key, bool shift, int maxW, int maxH)
    {
        if (el == null) return;
        int paso = shift ? Unidades.MmADmm(5) : Unidades.MmADmm(1);
        int limW = Math.Max(0, maxW - el.Ancho);
        int limH = Math.Max(0, maxH - el.Alto);
        (el.X, el.Y) = key switch
        {
            Keys.Left  => (Math.Max(0,    el.X - paso), el.Y),
            Keys.Right => (Math.Min(limW, el.X + paso), el.Y),
            Keys.Up    => (el.X, Math.Max(0,    el.Y - paso)),
            Keys.Down  => (el.X, Math.Min(limH, el.Y + paso)),
            _          => (el.X, el.Y),
        };
    }

    public void AplicarSnap(ElementoCanvas el)
    {
        el.X = Snap(el.X);
        el.Y = Snap(el.Y);
    }

    private static void NormZOrder(List<ElementoCanvas> lista)
    {
        int i = 0;
        foreach (var e in lista.OrderBy(x => x.ZOrder)) e.ZOrder = i++;
    }
}
