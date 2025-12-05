namespace Assignment1.Services.Email;

public class EmailOptions
{
    public string SenderAddress { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
}




