using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class TitleImageChangedMessage : ValueChangedMessage<string>
{
    public TitleImageChangedMessage(string newFileName) : base(newFileName)
    {
    }
}