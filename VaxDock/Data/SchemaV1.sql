CREATE TABLE IF NOT EXISTS Devices (
    Id              TEXT PRIMARY KEY,
    LastSeen        TEXT NOT NULL,
    OsVersion       TEXT,
    BiosString      TEXT
);

CREATE TABLE IF NOT EXISTS Scans (
    ScanId          TEXT PRIMARY KEY,
    DeviceId        TEXT NOT NULL REFERENCES Devices(Id),
    Timestamp       TEXT NOT NULL,
    PatchLevel      TEXT,
    RawJson         TEXT NOT NULL,
    Quarantined     INTEGER DEFAULT 0,
    IngestedAt      TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Findings (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    DeviceId        TEXT NOT NULL,
    CveId           TEXT NOT NULL,
    Severity        TEXT NOT NULL,
    Component       TEXT NOT NULL,
    Status          TEXT NOT NULL,
    RemediationId   TEXT,
    ResolvedAt      TEXT,
    EscalatedAt     TEXT,
    DefinitionsPackVersion TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PlcNeighbors (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    Ip              TEXT NOT NULL,
    Banner          TEXT,
    OpenPorts       TEXT
);

CREATE TABLE IF NOT EXISTS UsbAnomalies (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScanId          TEXT NOT NULL REFERENCES Scans(ScanId),
    DeviceId        TEXT NOT NULL,
    Description     TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS QuarantinedFiles (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    Filename        TEXT NOT NULL,
    FailureReason   TEXT NOT NULL,
    DetectedAt      TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_findings_device ON Findings(DeviceId);
CREATE INDEX IF NOT EXISTS idx_findings_severity ON Findings(Severity);
CREATE INDEX IF NOT EXISTS idx_scans_device ON Scans(DeviceId, Timestamp DESC);
