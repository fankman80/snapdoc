using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class PinChangedMessage(string pinId) : ValueChangedMessage<string>(pinId)
{
}
