using TwitchAPI.client.data;

namespace TwitchAPI.client.commands.data;

public class Command {
    public char CommandIdentifier { get; }
    public string CommandText { get; }
    public string CommandMessage { get; }
    public List<string> ArgumentsAsList { get; }
    public ChatMessage ChatMessage { get; }


    public Command(
        char commandIdentifier,
        string commandText,
        string commandMessage,
        List<string> argumentsAsList,
        ChatMessage chatMessage) {
        CommandIdentifier = commandIdentifier;
        CommandText = commandText;
        CommandMessage = commandMessage;
        ArgumentsAsList = argumentsAsList;
        ChatMessage = chatMessage;
    }
}