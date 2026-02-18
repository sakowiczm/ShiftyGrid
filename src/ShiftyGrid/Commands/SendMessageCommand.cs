using System.CommandLine;

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
        sendCommand.SetHandler(async message => await SendAsync(message), messageOption);

        return sendCommand;
    }

    private async Task SendAsync(string message) => await SendRequestAsync($"Sending message to running instance: {message}", Name, message);
}