using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class PinPropertyChangedMessage(string pinId, bool isLockPosition)
    : ValueChangedMessage<(string PinId, bool IsLockPosition)>((pinId, isLockPosition))
{
}