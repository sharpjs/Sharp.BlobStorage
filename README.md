# Sharp.BlobStorage

A very simple .NET interface to blob storage.  Supports these providers:

- The file system
- Azure Storage blob containers

## Status

[![Build](https://github.com/sharpjs/Sharp.BlobStorage/workflows/Build/badge.svg)](https://github.com/sharpjs/Sharp.BlobStorage/actions)

Available as NuGet packages:

- [Sharp.BlobStorage](https://www.nuget.org/packages/Sharp.BlobStorage)<br/>
  [![NuGet](https://img.shields.io/nuget/v/Sharp.BlobStorage.svg)](https://www.nuget.org/packages/Sharp.BlobStorage)
  [![NuGet](https://img.shields.io/nuget/dt/Sharp.BlobStorage.svg)](https://www.nuget.org/packages/Sharp.BlobStorage)

- [Sharp.BlobStorage.Azure](https://www.nuget.org/packages/Sharp.BlobStorage.Azure)<br/>
  [![NuGet](https://img.shields.io/nuget/v/Sharp.BlobStorage.Azure.svg)](https://www.nuget.org/packages/Sharp.BlobStorage.Azure)
  [![NuGet](https://img.shields.io/nuget/dt/Sharp.BlobStorage.Azure.svg)](https://www.nuget.org/packages/Sharp.BlobStorage.Azure)

Versions:
- 0.1.0 has been used in production for several years with no issues reported.
- 1.0.0 is in development.

## Usage

First, add the appriate `using` directives:

```csharp
using Sharp.BlobStorage;       // always
using Sharp.BlobStorage.File;  // to use the filesystem
using Sharp.BlobStorage.Azure; // to use Azure Storage
```

Next, create the appropriate blob storage client.  To store blobs as files in
the computer's file system:

```csharp
private IBlobStorage CreateBlobStorage()
{
    var configuration = new FileBlobStorageConfiguration
    {
        Path = @"C:\Blobs"
    };
    
    return new FileBlobStorage(configuration);
}
```

To store blobs in an Azure Storage blob container:

```csharp
private IBlobStorage CreateBlobStorage()
{
    var configuration = new AzureBlobStorageConfiguration
    {
        ConnectionString = @"...an Azure Storage connection string...",
        ContainerName    = @"blobs"
    };
    
    return new AzureBlobStorage(configuration);
}
```

And, to create the client:

```csharp
var storage = CreateBlobStorage();
```

### Operations

The Sharp.BlobStorage `IBlobStorage` interface exposes a minimal set of
operations, each with synchronous and asynchronous variants.

Synchronous | Asynchronous  | Description
------------|---------------|------------
`Put`       | `PutAsync`    | Creates (uploads) a blob
`Get`       | `GetAsync`    | Gets (downloads) a blob
`Delete`    | `DeleteAsync` | Deletes a blob

`IBlobStorage` methods identify each blob by a semi-random URI generated when
the blob is created.  Blob URIs are *not* specific to an underlying storage
provider.  Rather, blob URIs are generalized so that they can identify the same
blobs regardless of provider configuration changes.

The methods represent blob content as `Stream` objects, rather than strings
or byte arrays.  This is a conscious design decision intended to ~~force~~
encourage application authors to use a streaming approach when dealing with
blobs.  Streaming enables applications to handle arbitrarily large blobs without
exhausting available memory.

#### Creating a Blob

Given a readable `Stream` object and a filename extension, it is possible to
create (upload) a new blob storing the content of the stream.

```csharp
var uri = storage.Put(stream, ".txt");

// or

var uri = await storage.PutAsync(stream, ".txt");
```

The caller should save the returned URI, as it is the only way to identify the
blob.

`Put` and `PutAsync` do not dispose `stream`.

#### Retrieving a Blob

Given a blob URI, it is possible to retrieve (download) the blob's content as a
readable `Stream` object.

```csharp
using (var stream = storage.Get(uri))
{
    // read stream in here
}

// or

using (var stream = await storage.GetAsync(uri))
{
    // read stream in here
}
```

The caller should ensure that the stream returned by `Get` and `GetAsync` is
disposed when done.

#### Deleting a Blob

Given a blob URI, it is possible to delete the blob.

```csharp
var existed = storage.Delete(uri);

// or

var existed = await storage.DeleteAsync(uri);
```

The method succeeds regardless of whether the blob exists.  If the blob exists
and is deleted, the method returns `true`; otherwise, `false`.

## Notes

- To test Azure storage support, a working `docker` command is required.

