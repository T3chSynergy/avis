namespace Avis.Web.Domain;

/// <summary>Anmeldekonto. Jeder, der sich anmeldet, bekommt eines.</summary>
public class Benutzer
{
    public int Id { get; set; }

    /// <summary>Anmeldename in Kleinbuchstaben, bei Windows-Anmeldung "domäne\name".</summary>
    public string Anmeldename { get; set; } = "";

    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public bool IstBearbeiter { get; set; }
    public bool IstAdministrator { get; set; }
    public bool Aktiv { get; set; } = true;

    public List<Gruppe> Gruppen { get; } = [];
}

/// <summary>Gruppe von Bearbeitern für einen Aufgabenbereich.</summary>
public class Gruppe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Beschreibung { get; set; }

    /// <summary>In dieser Gruppe landen neue Aufträge, wenn nichts anderes gewählt wird.</summary>
    public bool IstEingangsgruppe { get; set; }

    public bool Aktiv { get; set; } = true;

    public List<Benutzer> Mitglieder { get; } = [];
}

/// <summary>Der Anwender, für den ein Auftrag bearbeitet wird.</summary>
public class Kunde
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public string? Telefon { get; set; }
    public string? Organisationseinheit { get; set; }
    public string? Anmeldename { get; set; }

    public int? StandortId { get; set; }
    public Standort? Standort { get; set; }

    /// <summary>Kennung in einem externen System, für einen späteren Abgleich.</summary>
    public string? ExterneKennung { get; set; }

    /// <summary>Name des externen Systems, aus dem der Eintrag stammt.</summary>
    public string? Quelle { get; set; }
}

public class Kategorie
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Aktiv { get; set; } = true;
}

public class Standort
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Aktiv { get; set; } = true;
}
