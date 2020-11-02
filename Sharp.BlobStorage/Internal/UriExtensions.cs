/*
    Copyright 2020 Jeffrey Sharp

    Permission to use, copy, modify, and distribute this software for any
    purpose with or without fee is hereby granted, provided that the above
    copyright notice and this permission notice appear in all copies.

    THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
    WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
    MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
    ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
    WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
    ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
    OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
*/

using System;

namespace Sharp.BlobStorage.Internal
{
    /// <summary>
    ///   Extension methods for <c>System.Uri</c>.
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        ///   Ensures that the path component of the current absolute URI ends
        ///   with a slash.
        /// </summary>
        /// <param name="uri">The URI to ensure.</param>
        /// <returns>
        ///   <paramref name="uri"/>, if <paramref name="uri"/>'s path
        ///   component already ends with a slash; otherwise, a copy of
        ///   <paramref name="uri"/> with a slash appended to the path
        ///   component.
        /// </returns>
        public static Uri EnsurePathTrailingSlash(this Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (!uri.IsAbsoluteUri)
                throw new ArgumentNullException(nameof(uri));

            if (uri.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
                return uri;

            var builder = new UriBuilder(uri);
            builder.Path += "/";
            return builder.Uri;
        }

        /// <summary>
        ///   Converts the current absolute URI to a relative URI by removing
        ///   the specified base URI.
        /// </summary>
        /// <param name="uri">
        ///   The absolute URI in which to remove the base URI.
        /// </param>
        /// <param name="baseUri">
        ///   The base URI to remove from <paramref name="uri"/>.
        /// </param>
        /// <returns>
        ///   A copy of <paramref name="uri"/> from which
        ///   <paramref name="baseUri"/> has been removed.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   One or more of
        ///     <paramref name="uri"/> or
        ///     <paramref name="baseUri"/>
        ///     is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   One or more of
        ///     <paramref name="uri"/> or
        ///     <paramref name="baseUri"/>
        ///     is a relative URI; or,
        ///   <paramref name="baseUri"/> is not a base of
        ///     <paramref name="uri"/>.
        /// </exception>
        public static Uri ToRelative(this Uri uri, Uri baseUri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (baseUri == null)
                throw new ArgumentNullException(nameof(baseUri));

            if (!uri.IsAbsoluteUri)
                throw UriNotAbsoluteError(uri, nameof(uri));
            if (!baseUri.IsAbsoluteUri)
                throw UriNotAbsoluteError(baseUri, nameof(baseUri));

            if (!baseUri.IsBaseOf(uri))
                throw UriNotRequiredBaseError(uri, baseUri, nameof(uri));

            return baseUri.MakeRelativeUri(uri);
        }

        /// <summary>
        ///   Replaces the specified base URI in the current absolute URI with
        ///   a new base URI.
        /// </summary>
        /// <param name="uri">
        ///   The absolute URI in which to replace the base URI.
        /// </param>
        /// <param name="oldBaseUri">
        ///   The old base URI to find in <paramref name="uri"/>.
        /// </param>
        /// <param name="newBaseUri">
        ///   The new base URI to insert in <paramref name="uri"/>> in place of
        ///   <paramref name="oldBaseUri"/>.
        /// </param>
        /// <returns>
        ///   A copy of <paramref name="uri"/> in which
        ///   <paramref name="oldBaseUri"/> has been replaced with
        ///   <paramref name="newBaseUri"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///   One or more of
        ///     <paramref name="uri"/>,
        ///     <paramref name="oldBaseUri"/>, or
        ///     <paramref name="newBaseUri"/>
        ///     is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   One or more of
        ///     <paramref name="uri"/>,
        ///     <paramref name="oldBaseUri"/>, or
        ///     <paramref name="newBaseUri"/>
        ///     is a relative URI; or,
        ///   <paramref name="oldBaseUri"/> is not a base of
        ///     <paramref name="uri"/>.
        /// </exception>
        public static Uri ChangeBase(this Uri uri, Uri oldBaseUri, Uri newBaseUri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (oldBaseUri == null)
                throw new ArgumentNullException(nameof(oldBaseUri));
            if (newBaseUri == null)
                throw new ArgumentNullException(nameof(newBaseUri));

            if (!uri.IsAbsoluteUri)
                throw UriNotAbsoluteError(uri, nameof(uri));
            if (!oldBaseUri.IsAbsoluteUri)
                throw UriNotAbsoluteError(oldBaseUri, nameof(oldBaseUri));
            if (!newBaseUri.IsAbsoluteUri)
                throw UriNotAbsoluteError(newBaseUri, nameof(newBaseUri));

            if (!oldBaseUri.IsBaseOf(uri))
                throw UriNotRequiredBaseError(uri, oldBaseUri, nameof(uri));

            var relativeUri = oldBaseUri.MakeRelativeUri(uri);
            return new Uri(newBaseUri, relativeUri);
        }

        /// <summary>
        ///   Creates an <c>ArgumentException</c> appropriate when the
        ///   specified URI is relative but is required to be absolute.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>An appropriate exception.</returns>
        public static ArgumentException UriNotAbsoluteError(
            Uri    uri,
            string parameterName)
        {
            return new ArgumentException(
                $"The URI '{uri}' is not an absolute URI.",
                parameterName
            );
        }

        /// <summary>
        ///   Creates an <c>ArgumentException</c> appropriate when the
        ///   specified URI does not have a required base URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="baseUri">The required base URI.</param>
        /// <param name="parameterName">The name of the parameter.</param>
        /// <returns>An appropriate exception.</returns>
        public static ArgumentException UriNotRequiredBaseError(
            Uri    uri,
            Uri    baseUri,
            string parameterName)
        {
            return new ArgumentException(
                $"The URI '{uri}' does not have the required base URI '{baseUri}'.",
                parameterName
            );
        }
    }
}
