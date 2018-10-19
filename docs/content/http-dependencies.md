# HTTP dependencies

Paket allows one to automatically manage the linking of files from HTTP
resources into your projects.

## Referencing a single file

You can reference a single file from an HTTP resource simply by specifying the
URL in the [`paket.dependencies` file](dependencies-file.html):

```paket
http http://www.fssnip.net/raw/1M/test1.fs
```

If you run the [`paket install` command](paket-install.html), it will add a new
section to your [`paket.lock` file](lock-file.html):

```paket
HTTP
  remote: http://www.fssnip.net/raw/1M/test1.fs
    test1.fs
```

If you want to reference the file in one of your project files then add an entry
to the project's [`paket.references` file.](references-files.html):

```paket
File: test1.fs
```

This will reference the linked file directly into your project. By default the
linked file will be visible under `paket-files` directory in project.

## Referencing a zip file

If you reference a zip file it will be unzipped relative to the downloaded zip file.

The reference

```
http file:///c:/projects/library.zip library/library.zip
```

will:

- write the zip file to `paket-files/localhost/library/library.zip`
- unzip the content to `paket-files/localhost/library`

## Build action conventions

The build action is determined depending on the file extension:

* If the file extension is equal to project type it is added as compile items. For
  instance `.cs` for `.csproj` projects and `.fs` for `.fsproj` projects.
* If  the file extension is `.dll` then it is added as reference.
* Otherwise it is added as an
  ['Add as link' content file](https://msdn.microsoft.com/en-us/library/windows/apps/jj714082(v=vs.105).aspx).

## Options for HTTP dependencies

When referencing a file using an `http` dependency, there are several options
that help you to deal with things like authentication and file name. The pattern
expected is:

```paket
    http <url> <file> <source name>
```

* `<file>` allows you to define the path to which the file that is downloaded
  will be written to. For example specifying the following

  ```paket
  http http://www.fssnip.net/raw/1M/test1.fs src/test1.fs
  ```

  will write the file to `paket-files/www.fssnip.net/src/test.fs`

* `<source name>` allows you to override the directory which the downloaded file
  is written to and also acts as a key to lookup any credentials that maybe
  associated for that key. For example you add credentials
  using [`paket config add-credentials MySource`](paket-config.html) then
  each time Paket extracts a HTTP dependency with `MySource` as a
  `<source name>` the credentials will be made part of the HTTP request. If no
  keys exist in the credential store then the request will be made without
  any `Authorization` headers.

## Allowed schemes

All `http://`, `https://` and `file://` URIs schemes are allowed. Examples:

* `http https://raw.githubusercontent.com/fsprojects/Paket/master/src/Paket.Core/ProjectFile.fs`
  will write the file to `paket-files/raw.githubusercontent.com/ProjectFile.fs`

* `http file:///c:/projects/library.dll`
  will write the file to `paket-files/localhost/library.dll`

## Updating HTTP dependencies

If you want to update a file you need to use the
[`paket install` command](paket-install.html) or
[`paket update` command](paket-update.html) with `--force` option.

Using [groups](groups.html) for HTTP-dependent files can be helpful to reduce
the number of files that are reinstalled.

## 
