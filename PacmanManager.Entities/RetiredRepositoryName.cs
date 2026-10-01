using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace PacmanManager.Entities;

/// <summary>
/// A name a repository gave up by being renamed. Pacman clients configured with the old name are
/// redirected to the repository's current name for a while, and are told the name is gone after
/// that; meanwhile nobody else may claim the name, so that those clients never silently start
/// syncing from somebody else's repository.
/// </summary>
/// <remarks>
/// <para>
/// A name is held while it is still redirecting, or while it has been requested within the
/// configured hold window, whichever is later. Release is lazy: nothing sweeps expired rows, and an
/// expired row is deleted when somebody claims its name.
/// </para>
/// <para>
/// Retired names and live repository names are one namespace as far as a claim is concerned: a name
/// is never both, since claiming a retired name deletes its row.
/// </para>
/// </remarks>
public record RetiredRepositoryName
{
    /// <summary>
    /// The retired name, matched exactly as stored, like a live repository's.
    /// </summary>
    [Key]
    [MaxLength(PacmanRepositoryValidationConstants.NameMaxLength)]
    public required string Name { get; init; }

    /// <summary>
    /// Id of the repository that gave the name up.
    /// </summary>
    [Required]
    public Guid RepositoryId { get; set; }

    /// <summary>
    /// The repository that gave the name up. Deleting it releases every name it once had, since
    /// there is nothing left to redirect to.
    /// </summary>
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public virtual PacmanRepository Repository { get; set; } = null!;

    /// <summary>
    /// When the repository was renamed away from this name.
    /// </summary>
    public DateTimeOffset RetiredAt { get; set; }

    /// <summary>
    /// When the old name stops redirecting to the repository's current name and starts answering
    /// <c>410 Gone</c>.
    /// </summary>
    public DateTimeOffset RedirectUntil { get; set; }

    /// <summary>
    /// When a client last asked for the name, which the sliding hold is computed from. Coarsely
    /// maintained: it is written only when the stored value is older than a configured resolution,
    /// so it may lag the true last request.
    /// </summary>
    public DateTimeOffset? LastRequestedAt { get; set; }

    /// <summary>
    /// Id of the user whose request last wrote <see cref="LastRequestedAt"/>, or <c>null</c> when that
    /// request was anonymous. A last-requester record, not an audit trail.
    /// </summary>
    public Guid? LastRequesterId { get; set; }

    /// <summary>
    /// The user whose request last wrote <see cref="LastRequestedAt"/>. Deleting the user forgets
    /// who asked, not that somebody did.
    /// </summary>
    [DeleteBehavior(DeleteBehavior.SetNull)]
    public virtual User? LastRequester { get; set; }
}
