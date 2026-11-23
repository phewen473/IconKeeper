// DesktopIcons.cs - reads and moves the icons on the Windows desktop.                                   // file purpose
// It talks to Explorer the supported way: ShellWindows -> desktop browser -> active view -> IFolderView2.  // how it works
// That is the same interface Explorer uses itself, so nothing pokes around inside Explorer's memory.       // why this way
using System.Diagnostics;                                                                                  // Stopwatch for the wait-for-desktop loop
using System.Runtime.InteropServices;                                                                      // COM interop attributes and Marshal helpers

namespace IconKeeper;                                                                                      // every class in this project lives here

/// <summary>One desktop icon as stored in a profile.</summary>                                           // doc comment
public sealed class IconPosition                                                                           // plain data record, saved to JSON
{                                                                                                          // start of IconPosition
    public string Key { get; set; } = "";                                                                  // unique shell name: full path, or ::{CLSID} for Recycle Bin / This PC
    public string Name { get; set; } = "";                                                                 // name shown under the icon (kept so the JSON is readable)
    public int X { get; set; }                                                                             // left edge, in desktop pixels
    public int Y { get; set; }                                                                             // top edge, in desktop pixels
}                                                                                                          // end of IconPosition

/// <summary>What happened during a restore, for the status line and the log.</summary>                   // doc comment
public sealed class RestoreResult                                                                          // counts reported back to the caller
{                                                                                                          // start of RestoreResult
    public int Moved;                                                                                      // icons placed where the profile says
    public int Missing;                                                                                    // icons in the profile that are no longer on the desktop
    public int NotInProfile;                                                                               // icons on the desktop the profile does not know (left where they are)
    public bool AutoArrangeWasOn;                                                                          // true if "Auto arrange icons" had to be switched off
    public override string ToString() =>                                                                   // one-line summary for messages and the log
        $"{Moved} moved, {Missing} in profile but not on desktop, {NotInProfile} on desktop but not in profile" // the three counts
        + (AutoArrangeWasOn ? " (Auto arrange icons was turned off)" : "");                                // plus a note if auto-arrange was turned off
}                                                                                                          // end of RestoreResult

/// <summary>Reads and sets desktop icon positions.</summary>                                              // doc comment
public static class DesktopIcons                                                                           // all methods are static; no state is kept
{                                                                                                          // start of DesktopIcons
    const int CSIDL_DESKTOP = 0;                                                                           // location code meaning "the desktop" for FindWindowSW
    const int SWC_DESKTOP = 8;                                                                             // ask ShellWindows for the desktop itself, not a folder window
    const int SWFO_NEEDDISPATCH = 1;                                                                       // we want an object back, not just a window handle
    const uint SVGIO_ALLVIEW = 2;                                                                          // count/enumerate every item in the view
    const uint SVSI_POSITIONITEM = 0x80;                                                                   // SelectAndPositionItems flag: just move the item
    const uint FWF_AUTOARRANGE = 0x1;                                                                      // folder flag for "Auto arrange icons"
    const uint SIGDN_NORMALDISPLAY = 0;                                                                    // ask for the friendly name shown under the icon
    const uint SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000;                                                  // ask for the unique full name of the item
    static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");               // service id of the desktop's top-level browser
    static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");                  // interface id of IShellBrowser
    static readonly Guid CLSID_ShellWindows = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");                 // class id of the ShellWindows object

    /// <summary>An icon as it is right now on the desktop, with its shell item id.</summary>             // doc comment
    sealed class LiveIcon                                                                                  // private helper record
    {                                                                                                      // start of LiveIcon
        public IntPtr Pidl;                                                                                // shell item id; must be freed with CoTaskMemFree
        public string Key = "";                                                                            // unique full name
        public string Name = "";                                                                           // friendly name
        public POINT Pos;                                                                                  // current position
    }                                                                                                      // end of LiveIcon

    /// <summary>Gets Explorer's desktop view. Throws if Explorer is not running yet.</summary>            // doc comment
    static IFolderView2 GetDesktopView()                                                                   // returns the COM view object of the desktop
    {                                                                                                      // start of GetDesktopView
        var type = Type.GetTypeFromCLSID(CLSID_ShellWindows, true)!;                                       // look up the ShellWindows COM class
        var windows = (IShellWindows)Activator.CreateInstance(type)!;                                      // create it (talks to Explorer)
        try                                                                                                // make sure we release it afterwards
        {                                                                                                  // start of try
            object loc = CSIDL_DESKTOP;                                                                    // "find the desktop" as a VARIANT (VT_I4)
            object? root = null;                                                                           // no root location (VT_EMPTY)
            object? disp = windows.FindWindowSW(ref loc!, ref root!, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH); // find the desktop window's automation object
            if (disp == null)                                                                              // Explorer has no desktop yet (early in login)
                throw new InvalidOperationException("The desktop is not ready yet (Explorer has not started)."); // tell the caller to try again later
            var provider = (IComServiceProvider)disp;                                                      // the desktop object is also a service provider
            Guid sid = SID_STopLevelBrowser, iid = IID_IShellBrowser;                                      // copies because QueryService takes them by ref
            var browser = (IShellBrowser)provider.QueryService(ref sid, ref iid);                          // get the desktop's shell browser
            object shellView = browser.QueryActiveShellView();                                             // get the view that shows the icons
            return (IFolderView2)shellView;                                                                // the view supports IFolderView2 (Windows 7 and later)
        }                                                                                                  // end of try
        finally                                                                                            // always runs
        {                                                                                                  // start of finally
            Marshal.ReleaseComObject(windows);                                                             // release ShellWindows; we hold the view instead
        }                                                                                                  // end of finally
    }                                                                                                      // end of GetDesktopView

    /// <summary>Returns a shell name for an item id, or "" if Windows cannot give one.</summary>          // doc comment
    static string NameOf(IntPtr pidl, uint sigdn)                                                          // pidl = desktop child item id, sigdn = which name
    {                                                                                                      // start of NameOf
        if (SHGetNameFromIDList(pidl, sigdn, out IntPtr text) != 0 || text == IntPtr.Zero) return "";      // failure -> empty name
        try { return Marshal.PtrToStringUni(text) ?? ""; }                                                 // copy the Unicode string into .NET
        finally { Marshal.FreeCoTaskMem(text); }                                                           // free the string Windows allocated
    }                                                                                                      // end of NameOf

    /// <summary>Reads every icon currently in the desktop view.</summary>                                 // doc comment
    static List<LiveIcon> ReadLive(IFolderView2 view)                                                      // caller must call Free() on the result
    {                                                                                                      // start of ReadLive
        view.ItemCount(SVGIO_ALLVIEW, out int count);                                                      // how many icons are on the desktop
        var list = new List<LiveIcon>(count);                                                              // room for all of them
        for (int i = 0; i < count; i++)                                                                    // walk every icon by index
        {                                                                                                  // start of loop body
            view.Item(i, out IntPtr pidl);                                                                 // get the icon's item id (we now own it)
            if (pidl == IntPtr.Zero) continue;                                                             // skip anything odd
            view.GetItemPosition(pidl, out POINT pt);                                                      // where it sits on screen
            var icon = new LiveIcon { Pidl = pidl, Pos = pt };                                             // start the record
            icon.Key = NameOf(pidl, SIGDN_DESKTOPABSOLUTEPARSING);                                         // unique full name
            icon.Name = NameOf(pidl, SIGDN_NORMALDISPLAY);                                                 // name under the icon
            list.Add(icon);                                                                                // keep it
        }                                                                                                  // end of loop body
        return list;                                                                                       // hand back everything we found
    }                                                                                                      // end of ReadLive

    /// <summary>Frees the item ids held in a ReadLive list.</summary>                                     // doc comment
    static void Free(List<LiveIcon> icons)                                                                 // releases unmanaged memory
    {                                                                                                      // start of Free
        foreach (var icon in icons) Marshal.FreeCoTaskMem(icon.Pidl);                                      // each pidl was allocated by Explorer for us
    }                                                                                                      // end of Free

    /// <summary>Returns the current position of every desktop icon.</summary>                            // doc comment
    public static List<IconPosition> Read()                                                                // used when saving a profile
    {                                                                                                      // start of Read
        var view = GetDesktopView();                                                                       // connect to the desktop
        var live = ReadLive(view);                                                                         // read all icons
        try                                                                                                // free memory even on error
        {                                                                                                  // start of try
            return live.Where(i => i.Key.Length > 0)                                                       // ignore icons without a usable name
                       .Select(i => new IconPosition { Key = i.Key, Name = i.Name, X = i.Pos.X, Y = i.Pos.Y }) // copy into the saved format
                       .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)                      // alphabetical, so the JSON is easy to read
                       .ToList();                                                                          // materialize before freeing the pidls
        }                                                                                                  // end of try
        finally                                                                                            // always runs
        {                                                                                                  // start of finally
            Free(live);                                                                                    // free the item ids
            Marshal.ReleaseComObject(view);                                                                // release the desktop view
        }                                                                                                  // end of finally
    }                                                                                                      // end of Read

    /// <summary>Moves desktop icons to the saved positions, multiplied by the scale factors.</summary>    // doc comment
    public static RestoreResult Apply(IEnumerable<IconPosition> saved, double scaleX, double scaleY)       // scale 1.0 = exact positions
    {                                                                                                      // start of Apply
        var view = GetDesktopView();                                                                       // connect to the desktop
        var live = ReadLive(view);                                                                         // what is on the desktop right now
        try                                                                                                // free memory even on error
        {                                                                                                  // start of try
            var result = new RestoreResult();                                                              // counts to report
            view.GetCurrentFolderFlags(out uint flags);                                                    // read the desktop's view flags
            if ((flags & FWF_AUTOARRANGE) != 0)                                                            // "Auto arrange icons" is on: it would undo our moves
            {                                                                                              // start of if
                view.SetCurrentFolderFlags(FWF_AUTOARRANGE, 0);                                            // switch auto-arrange off
                result.AutoArrangeWasOn = true;                                                            // remember to mention it
            }                                                                                              // end of if
            var byKey = new Dictionary<string, LiveIcon>(StringComparer.OrdinalIgnoreCase);                // live icons looked up by full name
            foreach (var icon in live)                                                                     // fill the lookup
                if (icon.Key.Length > 0) byKey.TryAdd(icon.Key, icon);                                     // first one wins if a name ever repeats
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);                            // every name the profile mentions
            var targets = new List<(LiveIcon Icon, POINT To)>();                                           // icons to move and where to
            foreach (var s in saved)                                                                       // walk the profile
            {                                                                                              // start of loop body
                wanted.Add(s.Key);                                                                         // note that the profile knows this icon
                if (!byKey.TryGetValue(s.Key, out var icon)) { result.Missing++; continue; }               // icon deleted or renamed since the save
                var to = new POINT { X = (int)Math.Round(s.X * scaleX), Y = (int)Math.Round(s.Y * scaleY) }; // target position, scaled if asked
                targets.Add((icon, to));                                                                   // queue the move
            }                                                                                              // end of loop body
            result.Moved = targets.Count;                                                                  // how many we are placing
            result.NotInProfile = byKey.Keys.Count(k => !wanted.Contains(k));                              // icons the profile does not cover
            for (int pass = 0; pass < 3 && targets.Count > 0; pass++)                                      // up to 3 passes: Explorer may nudge icons that collide mid-move
            {                                                                                              // start of pass
                var pidls = targets.Select(t => t.Icon.Pidl).ToArray();                                    // item ids to move
                var points = targets.Select(t => t.To).ToArray();                                          // matching target positions
                view.SelectAndPositionItems((uint)pidls.Length, pidls, points, SVSI_POSITIONITEM);         // move them all in one call
                Thread.Sleep(200);                                                                         // give Explorer a moment to settle
                targets = targets.Where(t => !IsAt(view, t.Icon.Pidl, t.To)).ToList();                     // keep only icons that did not land
            }                                                                                              // end of pass
            return result;                                                                                 // report the counts
        }                                                                                                  // end of try
        finally                                                                                            // always runs
        {                                                                                                  // start of finally
            Free(live);                                                                                    // free the item ids
            Marshal.ReleaseComObject(view);                                                                // release the desktop view
        }                                                                                                  // end of finally
    }                                                                                                      // end of Apply

    /// <summary>True if the icon is within 2 pixels of the target (snap-to-grid can shift it a little).</summary> // doc comment
    static bool IsAt(IFolderView2 view, IntPtr pidl, POINT to)                                             // used to check whether a move worked
    {                                                                                                      // start of IsAt
        view.GetItemPosition(pidl, out POINT now);                                                         // where the icon actually is
        return Math.Abs(now.X - to.X) <= 2 && Math.Abs(now.Y - to.Y) <= 2;                                 // close enough counts as placed
    }                                                                                                      // end of IsAt

    /// <summary>Waits (at login) until Explorer's desktop exists and its icon count stops changing.</summary> // doc comment
    public static bool WaitUntilReady(TimeSpan timeout)                                                    // true = ready, false = gave up
    {                                                                                                      // start of WaitUntilReady
        var clock = Stopwatch.StartNew();                                                                  // measure how long we have waited
        int lastCount = -1, sameCount = 0;                                                                 // previous icon count and how many times in a row it matched
        while (clock.Elapsed < timeout)                                                                    // keep trying until the time limit
        {                                                                                                  // start of loop body
            try                                                                                            // Explorer may not be up yet
            {                                                                                              // start of try
                var view = GetDesktopView();                                                               // connect to the desktop
                view.ItemCount(SVGIO_ALLVIEW, out int count);                                              // how many icons it shows
                Marshal.ReleaseComObject(view);                                                            // done with it for now
                sameCount = (count > 0 && count == lastCount) ? sameCount + 1 : 0;                         // count how long the number has been steady
                lastCount = count;                                                                         // remember for next time
                if (sameCount >= 2) return true;                                                           // steady for two checks (~4 s): icons have loaded
            }                                                                                              // end of try
            catch (Exception)                                                                              // desktop not available yet
            {                                                                                              // start of catch
                lastCount = -1; sameCount = 0;                                                             // start counting again
            }                                                                                              // end of catch
            Thread.Sleep(2000);                                                                            // check every two seconds
        }                                                                                                  // end of loop body
        return false;                                                                                      // timed out
    }                                                                                                      // end of WaitUntilReady

    [DllImport("shell32.dll")]                                                                             // Windows function in shell32.dll
    static extern int SHGetNameFromIDList(IntPtr pidl, uint sigdnName, out IntPtr ppszName);               // gets a display/parsing name for an item id
}                                                                                                          // end of DesktopIcons

// ---------------- COM declarations (layouts copied from the Windows SDK headers) ----------------        // section header

[StructLayout(LayoutKind.Sequential)]                                                                      // same memory layout as the Windows POINT struct
internal struct POINT                                                                                      // an x/y position in pixels
{                                                                                                          // start of POINT
    public int X;                                                                                          // horizontal position
    public int Y;                                                                                          // vertical position
}                                                                                                          // end of POINT

[ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)] // IShellWindows (exdisp.h); methods must stay in this order
internal interface IShellWindows                                                                           // list of open shell windows, including the desktop
{                                                                                                          // start of IShellWindows
    int Count { get; }                                                                                     // slot: get_Count (unused)
    [return: MarshalAs(UnmanagedType.IDispatch)] object Item([In, MarshalAs(UnmanagedType.Struct)] object index); // slot: Item (unused)
    [return: MarshalAs(UnmanagedType.IUnknown)] object _NewEnum();                                         // slot: _NewEnum (unused)
    int Register([MarshalAs(UnmanagedType.IDispatch)] object pid, int hwnd, int swClass);                  // slot: Register (unused)
    int RegisterPending(int threadId, [In, MarshalAs(UnmanagedType.Struct)] ref object loc, [In, MarshalAs(UnmanagedType.Struct)] ref object root, int swClass); // slot: RegisterPending (unused)
    void Revoke(int cookie);                                                                               // slot: Revoke (unused)
    void OnNavigate(int cookie, [In, MarshalAs(UnmanagedType.Struct)] ref object loc);                     // slot: OnNavigate (unused)
    void OnActivated(int cookie, [MarshalAs(UnmanagedType.VariantBool)] bool active);                      // slot: OnActivated (unused)
    [return: MarshalAs(UnmanagedType.IDispatch)]                                                           // the result is an automation object
    object? FindWindowSW([In, MarshalAs(UnmanagedType.Struct)] ref object loc,                             // slot: FindWindowSW - which location to find
                         [In, MarshalAs(UnmanagedType.Struct)] ref object root,                            // root location (empty)
                         int swClass, out int hwnd, int options);                                          // window class, returned handle, options
}                                                                                                          // end of IShellWindows

[ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // COM IServiceProvider (not the .NET one)
internal interface IComServiceProvider                                                                     // lets us ask an object for a related service
{                                                                                                          // start of IComServiceProvider
    [return: MarshalAs(UnmanagedType.IUnknown)] object QueryService([In] ref Guid service, [In] ref Guid riid); // slot: QueryService
}                                                                                                          // end of IComServiceProvider

[ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // IShellBrowser (shobjidl.h); order matters
internal interface IShellBrowser                                                                           // the desktop's browser object
{                                                                                                          // start of IShellBrowser
    [PreserveSig] int GetWindow(out IntPtr hwnd);                                                          // slot from IOleWindow (unused)
    [PreserveSig] int ContextSensitiveHelp(int enter);                                                     // slot from IOleWindow (unused)
    [PreserveSig] int InsertMenusSB(IntPtr menu, IntPtr widths);                                           // slot (unused)
    [PreserveSig] int SetMenuSB(IntPtr menu, IntPtr olemenu, IntPtr hwnd);                                 // slot (unused)
    [PreserveSig] int RemoveMenusSB(IntPtr menu);                                                          // slot (unused)
    [PreserveSig] int SetStatusTextSB(IntPtr text);                                                        // slot (unused)
    [PreserveSig] int EnableModelessSB(int enable);                                                        // slot (unused)
    [PreserveSig] int TranslateAcceleratorSB(IntPtr msg, short id);                                        // slot (unused)
    [PreserveSig] int BrowseObject(IntPtr pidl, uint flags);                                               // slot (unused)
    [PreserveSig] int GetViewStateStream(uint mode, out IntPtr stream);                                    // slot (unused)
    [PreserveSig] int GetControlWindow(uint id, out IntPtr hwnd);                                          // slot (unused)
    [PreserveSig] int SendControlMsg(uint id, uint msg, IntPtr wp, IntPtr lp, out IntPtr ret);             // slot (unused)
    [return: MarshalAs(UnmanagedType.IUnknown)] object QueryActiveShellView();                             // slot: the view showing the icons (we use this)
}                                                                                                          // end of IShellBrowser

[ComImport, Guid("1AF3A467-214F-4298-908E-06B03E0B39F9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] // IFolderView2 (shobjidl.h); order matters
internal interface IFolderView2                                                                            // the desktop's icon view
{                                                                                                          // start of IFolderView2
    void GetCurrentViewMode(out uint mode);                                                                // IFolderView slot (unused)
    void SetCurrentViewMode(uint mode);                                                                    // IFolderView slot (unused)
    void GetFolder(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object folder);                  // IFolderView slot (unused)
    void Item(int index, out IntPtr pidl);                                                                 // IFolderView: item id of icon number "index"
    void ItemCount(uint flags, out int count);                                                             // IFolderView: number of icons
    void Items(uint flags, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object items);           // IFolderView slot (unused)
    void GetSelectionMarkedItem(out int item);                                                             // IFolderView slot (unused)
    void GetFocusedItem(out int item);                                                                     // IFolderView slot (unused)
    void GetItemPosition(IntPtr pidl, out POINT pt);                                                       // IFolderView: where an icon is
    void GetSpacing(out POINT pt);                                                                         // IFolderView slot (unused)
    void GetDefaultSpacing(out POINT pt);                                                                  // IFolderView slot (unused)
    [PreserveSig] int GetAutoArrange();                                                                    // IFolderView slot (unused)
    void SelectItem(int item, uint flags);                                                                 // IFolderView slot (unused)
    void SelectAndPositionItems(uint count,                                                                // IFolderView: move icons - how many
        [MarshalAs(UnmanagedType.LPArray)] IntPtr[] pidls,                                                 // which icons
        [MarshalAs(UnmanagedType.LPArray)] POINT[] points, uint flags);                                    // where to, and how
    [PreserveSig] int SetGroupBy(IntPtr key, int ascending);                                               // IFolderView2 slot (unused)
    [PreserveSig] int GetGroupBy(IntPtr key, out int ascending);                                           // IFolderView2 slot (unused)
    [PreserveSig] int SetViewProperty(IntPtr pidl, IntPtr key, IntPtr value);                              // IFolderView2 slot (unused)
    [PreserveSig] int GetViewProperty(IntPtr pidl, IntPtr key, IntPtr value);                              // IFolderView2 slot (unused)
    [PreserveSig] int SetTileViewProperties(IntPtr pidl, IntPtr list);                                     // IFolderView2 slot (unused)
    [PreserveSig] int SetExtendedTileViewProperties(IntPtr pidl, IntPtr list);                             // IFolderView2 slot (unused)
    [PreserveSig] int SetText(int type, IntPtr text);                                                      // IFolderView2 slot (unused)
    void SetCurrentFolderFlags(uint mask, uint flags);                                                     // IFolderView2: change view flags (auto-arrange)
    void GetCurrentFolderFlags(out uint flags);                                                            // IFolderView2: read view flags
}                                                                                                          // end of IFolderView2
