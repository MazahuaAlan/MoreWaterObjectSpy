using System.Reflection;
using System.Text;
using MoreWaterObjectSpy.Core;
using MoreWaterObjectSpy.Infrastructure;

namespace MoreWaterObjectSpy.Inspectors;

/// <summary>
/// Inspector MSAA (IAccessible / oleacc). Consulta por punto de pantalla.
/// Usa late-binding (IDispatch) sobre el objeto COM para evitar declarar la vtable de IAccessible.
/// Es la red de seguridad para Delphi/VCL, donde UI Automation a veces no expone bien el control.
/// </summary>
public static class MSAAInspector
{
    public static MSAAInfo Inspect(int x, int y)
    {
        var info = new MSAAInfo();
        try
        {
            var pt = new NativeMethods.POINT { X = x, Y = y };
            uint hr = NativeMethods.AccessibleObjectFromPoint(pt, out object acc, out object child);
            if (hr != 0 || acc == null)
            {
                info.Available = false;
                info.Error = $"AccessibleObjectFromPoint HRESULT=0x{hr:X8}";
                return info;
            }

            child ??= 0; // CHILDID_SELF
            info.Available = true;
            info.Name = GetStr(acc, "accName", child);
            info.Value = GetStr(acc, "accValue", child);
            info.Description = GetStr(acc, "accDescription", child);
            info.DefaultAction = GetStr(acc, "accDefaultAction", child);
            info.Role = RoleText(Get(acc, "accRole", child));
            info.State = StateText(Get(acc, "accState", child));
        }
        catch (Exception ex)
        {
            info.Available = false;
            info.Error = ex.Message;
        }
        return info;
    }

    private static object? Get(object acc, string prop, object child)
    {
        try
        {
            return acc.GetType().InvokeMember(prop, BindingFlags.GetProperty, null, acc, new[] { child });
        }
        catch { return null; }
    }

    private static string GetStr(object acc, string prop, object child)
        => Get(acc, prop, child) as string ?? "";

    private static string RoleText(object? role)
    {
        if (role == null) return "";
        try
        {
            uint r = Convert.ToUInt32(role);
            var sb = new StringBuilder(128);
            uint n = NativeMethods.GetRoleText(r, sb, (uint)sb.Capacity);
            return n > 0 ? sb.ToString() : $"role#{r}";
        }
        catch { return role.ToString() ?? ""; }
    }

    private static string StateText(object? state)
    {
        if (state == null) return "";
        try
        {
            uint s = Convert.ToUInt32(state);
            var parts = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                uint bit = 1u << i;
                if ((s & bit) == 0) continue;
                var sb = new StringBuilder(128);
                uint n = NativeMethods.GetStateText(bit, sb, (uint)sb.Capacity);
                if (n > 0) parts.Add(sb.ToString());
            }
            return string.Join(", ", parts);
        }
        catch { return state.ToString() ?? ""; }
    }
}
