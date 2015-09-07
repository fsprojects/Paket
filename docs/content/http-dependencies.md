# HTTP dependencies

Paket allows one to automatically manage the linking of files from HTTP resources into your projects.

## Referencing a single file

You can reference a single file from a HTTP resource simply by specifying the url in the [`paket.dependencies` file](dependencies-file.html):

    http http://www.fssnip.net/raw/1M/test1.fs

If you run the [`paket update` command](paket-update.html), it will add a new section to your [`paket.lock` file](lock-file.html):

    HTTP
      remote: http://www.fssnip.net/raw/1M/test1.fs
      specs:
        test1.fs


If you want to reference the file in one of your project files then add an entry to the project's [`paket.references` file.](references-files.html):
    
    File:test1.fs

This will reference the linked file directly into your project.
By default the linked file will be visible under ``paket-files`` folder in project.

## Options on http dependencies

When referencing a file using a http dependency, there are several options that help you to deal with things like authentication and file name. 
The pattern expected is 

	http url [FileSpec] [SourceName]

* **FileSpec** - This allows you to define the path to which the file that is downloaded will be written to. For example specfying the following

		http http://www.fssnip.net/raw/1M/test1.fs src/test1.fs

	will write the file to `paket-files\www.fssnip.net\src\test.fs` 

* **SourceName** - The source name allows you to override the folder which the downloaded file is written to and also acts as a key to lookup any authentication 
that maybe assoicated for that key. For example if I had added an authentication source using [``paket config add-authentication MySource``](commands\config.html)
then each time paket extracts a HTTP dependency with `MySource` as a `SourceName` the credentails will be made part of the HTTP request. If no keys exist in the authentication store
then the request will be made without any authentication headers.