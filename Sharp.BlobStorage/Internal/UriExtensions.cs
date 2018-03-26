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
