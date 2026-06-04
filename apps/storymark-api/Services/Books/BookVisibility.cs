using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Books;

namespace Storymark.Api.Services.Books;

/// <summary>Outcome of an owner-gated book mutation (ADR-0023). Delete and the
/// visibility flip share the contract: a book outside the caller's visibility is
/// a 404, a visible book owned by someone else is a 403.</summary>
internal enum OwnedBookOutcome
{
    Succeeded,

    /// <summary>No such book, or not visible to the caller — indistinguishable.</summary>
    NotFound,

    /// <summary>Visible (public) but owned by someone else; only the owner mutates.</summary>
    NotOwned,
}

/// <summary>The single visibility seam for per-user books (ADR-0023): a reader
/// sees their own books plus public ones. Every book-scoped reader read path
/// goes through this — a book outside it is a 404, indistinguishable from
/// absent. Jobs and admin services deliberately bypass it.</summary>
internal static class BookVisibility
{
    public static IQueryable<Book> VisibleTo(this IQueryable<Book> books, Guid userId) =>
        books.Where(b => b.OwnerUserId == userId || b.IsPublic);

    /// <summary>The visibility-existence gate: whether the book exists *for this
    /// reader*. Services answer "no" identically for absent and non-visible books.</summary>
    public static Task<bool> IsVisibleAsync(
        this IQueryable<Book> books, Guid bookId, Guid userId, CancellationToken ct) =>
        books.VisibleTo(userId).AnyAsync(b => b.Id == bookId, ct);

    /// <summary>The single owner gate for book mutations: resolves the tracked
    /// book and maps it onto the shared <see cref="OwnedBookOutcome"/> contract.
    /// The book is non-null exactly when the outcome is
    /// <see cref="OwnedBookOutcome.Succeeded"/>.</summary>
    public static async Task<(OwnedBookOutcome Outcome, Book? Book)> ResolveOwnedAsync(
        this IQueryable<Book> books, Guid bookId, Guid userId, CancellationToken ct)
    {
        var book = await books.VisibleTo(userId).FirstOrDefaultAsync(b => b.Id == bookId, ct);
        if (book is null)
        {
            return (OwnedBookOutcome.NotFound, null);
        }

        return book.OwnerUserId != userId
            ? (OwnedBookOutcome.NotOwned, null)
            : (OwnedBookOutcome.Succeeded, book);
    }
}
