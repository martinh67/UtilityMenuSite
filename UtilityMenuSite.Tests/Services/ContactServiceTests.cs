using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UtilityMenuSite.Core.Interfaces;
using UtilityMenuSite.Core.Models;
using UtilityMenuSite.Data.Models;
using UtilityMenuSite.Services.Contact;

namespace UtilityMenuSite.Tests.Services;

/// <summary>
/// Unit tests for ContactService: honeypot spam detection, IP-based rate limiting,
/// successful submission, and resolution flow.
/// </summary>
public class ContactServiceTests
{
    private readonly Mock<IContactRepository>      _repoMock   = new();
    private readonly Mock<ILogger<ContactService>> _loggerMock = new();

    private ContactService CreateSut() => new(_repoMock.Object, _loggerMock.Object);

    private static ContactFormDto ValidDto(string? honeypot = null) => new()
    {
        Name    = "Martin Hannah",
        Email   = "martin@example.com",
        Subject = "General Enquiry",
        Message = "This is a test message with enough characters to pass validation.",
        Website = honeypot
    };

    // ── Honeypot ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_WhenHoneypotFilled_ReturnsTrueWithoutSaving()
    {
        // Bot filled the hidden "Website" field — silently accept but don't save.
        var dto = ValidDto(honeypot: "http://spam-bot.example.com");

        var result = await CreateSut().SubmitAsync(dto, ipAddress: "1.2.3.4");

        result.Should().BeTrue("we return true to not reveal the honeypot check to bots");
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WhenHoneypotIsWhitespace_TreatsAsEmptyAndProceedsNormally()
    {
        // ContactService uses !string.IsNullOrWhiteSpace — whitespace-only is treated as
        // empty (no bot), so the submission is processed normally.
        var dto = ValidDto(honeypot: "   ");

        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactSubmission());

        var result = await CreateSut().SubmitAsync(dto, ipAddress: "1.2.3.4");

        result.Should().BeTrue();
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_WhenHoneypotIsEmpty_ProceedsNormally()
    {
        var dto = ValidDto(honeypot: null);

        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactSubmission());

        var result = await CreateSut().SubmitAsync(dto, ipAddress: "1.2.3.4");

        result.Should().BeTrue();
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Rate limiting ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_WhenAtExactRateLimit_ReturnsFalse()
    {
        // Rate limit is 3 per hour — count of 3 means limit reached.
        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await CreateSut().SubmitAsync(ValidDto(), ipAddress: "10.0.0.1");

        result.Should().BeFalse();
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Submit_WhenOverRateLimit_ReturnsFalse()
    {
        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var result = await CreateSut().SubmitAsync(ValidDto(), ipAddress: "10.0.0.2");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Submit_WhenUnderRateLimit_Succeeds()
    {
        // 2 previous submissions — one more is still allowed.
        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactSubmission());

        var result = await CreateSut().SubmitAsync(ValidDto(), ipAddress: "10.0.0.3");

        result.Should().BeTrue();
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Submit_WhenIpAddressIsNull_SkipsRateLimitCheckAndSaves()
    {
        // No IP available (proxy / forwarding edge case) — skip rate limit, save anyway.
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactSubmission());

        var result = await CreateSut().SubmitAsync(ValidDto(), ipAddress: null);

        result.Should().BeTrue();
        _repoMock.Verify(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Submission content ────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_WhenValid_SavesSubmissionWithCorrectFields()
    {
        ContactSubmission? saved = null;
        _repoMock.Setup(r => r.CountByIpSinceAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()))
            .Callback<ContactSubmission, CancellationToken>((s, _) => saved = s)
            .ReturnsAsync(new ContactSubmission());

        var dto = new ContactFormDto
        {
            Name    = "Test User",
            Email   = "test@example.com",
            Subject = "Billing",
            Message = "I have a question about my invoice that is longer than twenty chars."
        };

        await CreateSut().SubmitAsync(dto, ipAddress: "5.5.5.5");

        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test User");
        saved.Email.Should().Be("test@example.com");
        saved.Subject.Should().Be("Billing");
        saved.IpAddress.Should().Be("5.5.5.5");
        saved.IsResolved.Should().BeFalse();
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_WhenSubmissionExists_SetsIsResolvedAndResolvedAt()
    {
        var submissionId = Guid.NewGuid();
        var submission = new ContactSubmission
        {
            SubmissionId = submissionId,
            IsResolved   = false,
            ResolvedAt   = null
        };

        _repoMock.Setup(r => r.GetByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var before = DateTime.UtcNow;
        var result = await CreateSut().ResolveAsync(submissionId, notes: "Handled via email");
        var after  = DateTime.UtcNow;

        result.Should().BeTrue();
        submission.IsResolved.Should().BeTrue();
        submission.Notes.Should().Be("Handled via email");
        submission.ResolvedAt.Should().NotBeNull();
        submission.ResolvedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        _repoMock.Verify(r => r.UpdateAsync(submission, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenSubmissionNotFound_ReturnsFalse()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactSubmission?)null);

        var result = await CreateSut().ResolveAsync(Guid.NewGuid(), notes: null);

        result.Should().BeFalse();
        _repoMock.Verify(r => r.UpdateAsync(It.IsAny<ContactSubmission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_WithNullNotes_StillResolvesSuccessfully()
    {
        var submissionId = Guid.NewGuid();
        var submission = new ContactSubmission { SubmissionId = submissionId };

        _repoMock.Setup(r => r.GetByIdAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var result = await CreateSut().ResolveAsync(submissionId, notes: null);

        result.Should().BeTrue();
        submission.IsResolved.Should().BeTrue();
        submission.Notes.Should().BeNull();
    }

    // ── GetPendingSubmissionsAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetPendingSubmissionsAsync_DelegatesToRepository()
    {
        var pending = new List<ContactSubmission>
        {
            new() { SubmissionId = Guid.NewGuid(), Name = "User A" },
            new() { SubmissionId = Guid.NewGuid(), Name = "User B" }
        };

        _repoMock.Setup(r => r.GetPendingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);

        var result = await CreateSut().GetPendingSubmissionsAsync();

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(pending);
    }
}
