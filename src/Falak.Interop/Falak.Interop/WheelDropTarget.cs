using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Falak.Core;
using Falak.Interop.Native;

namespace Falak.Interop;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class WheelDropTarget : IOleDropTarget
{
	private delegate void EffectStep(ref int effect);

	private readonly IWheelSurface _surface;

	private readonly Action<string> _log;

	private readonly IDropTargetHelper? _imageHelper;

	private DroppedFiles _files = DroppedFiles.Empty;

	public DropEffect LastAnswer { get; private set; }

	public WheelDropTarget(IWheelSurface surface, Action<string> log, bool useDragImageHelper = true)
	{
		_surface = surface;
		_log = log;
		if (useDragImageHelper)
		{
			_imageHelper = CreateImageHelper(log);
		}
	}

	private static IDropTargetHelper? CreateImageHelper(Action<string> log)
	{
		try
		{
			return (IDropTargetHelper)Activator.CreateInstance(Type.GetTypeFromCLSID(ComIds.CLSID_DragDropHelper, throwOnError: true));
		}
		catch (Exception ex)
		{
			log("Drag image helper unavailable: " + ex.Message);
			return null;
		}
	}

	private static POINT Unpack(long pt)
	{
		return new POINT((int)(pt & 0xFFFFFFFFu), (int)(pt >> 32));
	}

	int IOleDropTarget.DragEnter(object pDataObj, int ignoredKeyState, long pt, ref int pdwEffect)
	{
		return Guard("DragEnter", ref pdwEffect, delegate(ref int effect)
		{
			POINT ppt = Unpack(pt);
			_files = DataObjectReader.Read(pDataObj as IDataObject);
			_surface.OnDragEntered(_files);
			WheelHit hit = _surface.HitTestScreen(ppt.X, ppt.Y);
			_surface.OnHover(hit);
			effect = Answer(hit, effect);
			_imageHelper?.DragEnter(_surface.Hwnd, pDataObj, ref ppt, effect);
		});
	}

	int IOleDropTarget.DragOver(int ignoredKeyState, long pt, ref int pdwEffect)
	{
		return Guard("DragOver", ref pdwEffect, delegate(ref int effect)
		{
			POINT ppt = Unpack(pt);
			WheelHit hit = _surface.HitTestScreen(ppt.X, ppt.Y);
			_surface.OnHover(hit);
			effect = Answer(hit, effect);
			_imageHelper?.DragOver(ref ppt, effect);
		});
	}

	int IOleDropTarget.DragLeave()
	{
		try
		{
			_imageHelper?.DragLeave();
			_surface.OnDragLeft();
		}
		catch (Exception ex)
		{
			_log("DragLeave failed: " + ex.Message);
		}
		_files = DroppedFiles.Empty;
		return 0;
	}

	int IOleDropTarget.Drop(object pDataObj, int ignoredKeyState, long pt, ref int pdwEffect)
	{
		return Guard("Drop", ref pdwEffect, delegate(ref int effect)
		{
			POINT ppt = Unpack(pt);
			WheelHit hit = _surface.HitTestScreen(ppt.X, ppt.Y);
			effect = Answer(hit, effect);
			DroppedFiles files = ((_files.Count > 0) ? _files : DataObjectReader.Read(pDataObj as IDataObject));
			if (effect != 0 && pDataObj is IDataObject data)
			{
				DataObjectReader.MarkPerformedEffect(data, (uint)effect);
			}
			_imageHelper?.Drop(pDataObj, ref ppt, effect);
			_files = DroppedFiles.Empty;
			_surface.OnDropped(hit, files, (DropEffect)effect);
		});
	}

	private int Answer(WheelHit hit, int allowedBySource)
	{
		return (int)(LastAnswer = DropEffectPolicy.ForHit(hit, (DropEffect)allowedBySource));
	}

	private int Guard(string step, ref int effect, EffectStep body)
	{
		try
		{
			body(ref effect);
		}
		catch (Exception ex)
		{
			effect = 0;
			LastAnswer = DropEffect.None;
			_log(step + " failed, drop refused: " + ex.Message);
		}
		return 0;
	}
}
