using System;
using System.Runtime.InteropServices;

namespace LibAlpmSharp.Interop;

/// <summary>
/// Event callback (alpm_cb_event)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="event">the event that occurred</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmCbEvent(void* ctx, void* @event);

/// <summary>
/// Question callback (alpm_cb_question)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="question">the question being asked</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmCbQuestion(void* ctx, void* question);

/// <summary>
/// Progress callback (alpm_cb_progress)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="progress">the kind of event that is progressing</param>
/// <param name="pkg">for package operations, the name of the package being operated on</param>
/// <param name="percent">the percent completion of the action</param>
/// <param name="howmany">the total amount of items in the action</param>
/// <param name="current">the current amount of items completed</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmCbProgress(void* ctx, AlpmProgress progress, byte* pkg, int percent, nuint howmany, nuint current);

/// <summary>
/// Download callback (alpm_cb_download)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="filename">the name of the file being downloaded</param>
/// <param name="event">the event type</param>
/// <param name="data">the event data of type alpm_download_event_*_t</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmCbDownload(void* ctx, byte* filename, AlpmDownloadEventType @event, void* data);

/// <summary>
/// Fetch callback (alpm_cb_fetch)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="url">the URL of the file to be downloaded</param>
/// <param name="localpath">the directory to which the file should be downloaded</param>
/// <param name="force">whether to force an update, even if the file is the same</param>
/// <returns>0 on success, 1 if the file exists and is identical, -1 on error</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int AlpmCbFetch(void* ctx, byte* url, byte* localpath, int force);

/// <summary>
/// Log callback (alpm_cb_log)
/// </summary>
/// <param name="ctx">user-provided context</param>
/// <param name="level">the currently set loglevel</param>
/// <param name="fmt">the printf like format string</param>
/// <param name="args">printf like arguments</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmCbLog(void* ctx, AlpmLogLevel level, byte* fmt, IntPtr args);

/// <summary>
/// List item deallocation callback (alpm_list_fn_free)
/// </summary>
/// <param name="item">the item to free</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void AlpmListFnFree(void* item);

/// <summary>
/// List item comparison callback (alpm_list_fn_cmp)
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate int AlpmListFnCmp(void* a, void* b);
