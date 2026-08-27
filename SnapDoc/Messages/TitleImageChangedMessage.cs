using CommunityToolkit.Mvvm.Messaging.Messages;

namespace SnapDoc.Messages;

public class TitleImageChangedMessage
{
    public string OldFileName { get; }
    public string NewFileName { get; }

    public TitleImageChangedMessage(string oldFileName, string newFileName)
    {
        OldFileName = oldFileName;
        NewFileName = newFileName;
    }
}