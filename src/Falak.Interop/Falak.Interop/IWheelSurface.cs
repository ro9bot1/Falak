using Falak.Core;

namespace Falak.Interop;

public interface IWheelSurface
{
	nint Hwnd { get; }

	WheelHit HitTestScreen(int x, int y);

	void OnDragEntered(DroppedFiles files);

	void OnHover(WheelHit hit);

	void OnDragLeft();

	void OnDropped(WheelHit hit, DroppedFiles files, DropEffect answered);
}
