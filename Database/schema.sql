-- Tanariri Musical Museum - ticket booking schema (SQL Server)
IF DB_ID('TanaririMuseum') IS NULL CREATE DATABASE TanaririMuseum;
GO
USE TanaririMuseum;
GO
IF OBJECT_ID('dbo.BookingTickets') IS NOT NULL DROP TABLE dbo.BookingTickets;
IF OBJECT_ID('dbo.Bookings') IS NOT NULL DROP TABLE dbo.Bookings;
GO
CREATE TABLE dbo.Bookings (
    BookingId     INT IDENTITY(1,1) PRIMARY KEY,
    Mobile        VARCHAR(15)   NOT NULL,
    Otp           VARCHAR(6)    NULL,
    OtpExpiresAt  DATETIME2     NULL,
    OtpAttempts   INT           NOT NULL DEFAULT 0,
    OtpVerified   BIT           NOT NULL DEFAULT 0,
    VisitorName   NVARCHAR(150) NULL,
    Email         NVARCHAR(200) NULL,
    City          NVARCHAR(100) NULL,
    State         NVARCHAR(100) NULL,
    Country       NVARCHAR(60)  NULL,
    VisitDate     DATE          NULL,
    SlotTime      CHAR(5)       NULL,
    TotalGuests   INT           NOT NULL DEFAULT 0,
    TotalAmount   DECIMAL(10,2) NOT NULL DEFAULT 0,
    PaymentStatus VARCHAR(20)   NOT NULL DEFAULT 'pending',
    PaymentMode   VARCHAR(50)   NULL,
    EasePayId     VARCHAR(60)   NULL,
    BankReference VARCHAR(60)   NULL,
    PaidAmount    DECIMAL(10,2) NULL,
    Pnr           VARCHAR(30)   NULL,
    CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE TABLE dbo.BookingTickets (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    BookingId    INT NOT NULL REFERENCES dbo.Bookings(BookingId),
    CategoryCode VARCHAR(30)   NOT NULL,
    CategoryName NVARCHAR(120) NOT NULL,
    Quantity     INT           NOT NULL,
    Rate         DECIMAL(10,2) NOT NULL,
    Amount       DECIMAL(10,2) NOT NULL
);
CREATE INDEX IX_Bookings_Slot   ON dbo.Bookings (VisitDate, SlotTime, PaymentStatus) INCLUDE (TotalGuests, UpdatedAt);
CREATE INDEX IX_Bookings_Lookup ON dbo.Bookings (Mobile, Email);
CREATE INDEX IX_Tickets_Booking ON dbo.BookingTickets (BookingId);
GO
