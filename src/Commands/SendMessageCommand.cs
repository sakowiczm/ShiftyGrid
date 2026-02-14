using System.CommandLine;
using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

public class SendMessageCommand : BaseCommand
{
    public const string Name = "send";
    
    public Command Create()
    {
        var sendCommand = new Command(Name, "Send a message to the running instance");
        var messageOption = new Option<string>(
            aliases: ["--message", "-m"],
            description: "Message to send to the running instance")
        {
            IsRequired = true
        };

        sendCommand.AddOption(messageOption);
        sendCommand.SetHandler(message => Send(message), messageOption);

        return sendCommand;
    }

    private void Send(string message)
    {
        SendRequest(
            $"Sending message to running instance: {message}",
            new Request
            {
                Command = Name,
                Args = [message]
            }
        );
    }
}