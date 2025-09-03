using System.Text;
using TwitchAPI.client.commands.data;
using TwitchAPI.client.data;

namespace TwitchAPI.client.commands;

public class CommandParser {
    private const char DefaultIdentifier = '!';

    private char _commandIdentifier;
    
    
    public CommandParser(char? identifier = null) {
        _commandIdentifier = identifier ?? DefaultIdentifier;
    }
    
    public Command? Parse(ChatMessage chatMessage) {
        var commandIdentifier = ' ';

        var commandMessage = chatMessage.Text;

        if (commandMessage.Length < 1) {
            return null;
        }

        if (char.IsPunctuation(commandMessage[0])) {
            commandIdentifier = commandMessage[0];
        }

        if (commandIdentifier != ' ' && commandIdentifier != _commandIdentifier) {
            return null;
        }
        
        var startIndex =
            commandIdentifier == ' ' ? 0 : 1;
        
        if (commandMessage.Length <= startIndex) {
            return null;
        }

        var commandText = GetCommandText(startIndex, commandMessage, out var end);

        if (string.IsNullOrEmpty(commandText)) {
            return null;
        }

        commandMessage = GetCommandMessage(end, commandMessage).Trim();
        var argumentsAsList = commandMessage.Split(" ", StringSplitOptions.TrimEntries).ToList();
        argumentsAsList.RemoveAll(x => x.Equals(""));
        
        return new Command(
                           commandIdentifier, 
                           commandText,
                           commandMessage,
                           argumentsAsList,
                           chatMessage
                          );
    }

    public bool SetCommandIdentifier(char identifier) {
        if (!char.IsPunctuation(identifier)) return false;
        
        _commandIdentifier = identifier;
        return true;
    }
    
    private static string GetCommandText(int start, string message, out int end) {
        var sb = new StringBuilder();
        var index = start;

        for (; index < message.Length; ++index) {
            if (message[index] == ' ') {
                break;
            }

            sb.Append(message[index]);
        }

        end = index;
        return sb.ToString();
    }
    
    private static string GetCommandMessage(int start, string message) {
        var sb = new StringBuilder();

        for (var i = start; i < message.Length; ++i) {
            sb.Append(message[i]);
        }

        return sb.ToString();
    }
}