namespace Avis.Web.Mail;

public class MailOptionen
{
    public const string Aus = "Aus";
    public const string Smtp = "Smtp";
    public const string Ordner = "Ordner";

    /// <summary>
    /// "Aus": Es wird nichts gesendet. "Smtp": Versand über einen Mailserver.
    /// "Ordner": Jede Mail wird als Datei abgelegt, zum Ausprobieren ohne Mailserver.
    /// </summary>
    public string Art { get; set; } = Aus;

    public string? Server { get; set; }
    public int Port { get; set; } = 25;

    /// <summary>"Auto", "Keine", "StartTls" oder "Ssl".</summary>
    public string Verschluesselung { get; set; } = "Auto";

    /// <summary>Leer lassen, wenn der Mailserver keine Anmeldung verlangt.</summary>
    public string? Benutzer { get; set; }
    public string? Kennwort { get; set; }

    public string? Absender { get; set; }
    public string Absendername { get; set; } = "IT-Hotline";

    /// <summary>Adresse von AVIS für Links in Mails, zum Beispiel https://avis.example.org</summary>
    public string? BasisUrl { get; set; }

    /// <summary>Ablageort für die Art "Ordner", relativ zum Ordner der Anwendung.</summary>
    public string Ablage { get; set; } = "App_Data/Mails";

    public bool ImHintergrundSenden { get; set; } = true;
    public int AbstandSekunden { get; set; } = 15;
}
