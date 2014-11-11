# HTTP dependencies

Paket allows one to automatically manage the linking of files from HTTP resources into your projects.

## Referencing a single file

You can reference a single file from a HTTP resource simply by specifying the url in the [`paket.dependencies` file](dependencies-file.html):

    http http://www.fssnip.net/raw/1M test1.fs

If you run the [`paket update` command](paket-update.html), it will add a new section to your [`paket.lock` file](lock-file.html):

    HTTP
      remote: http://www.fssnip.net/raw/1M
      specs:
        test1.fs


If you want to reference the file in one of your project files then add an entry to the project's [`paket.references` file.](references-files.html):
    
    File:test1.fs

This will reference the linked file directly into your project.
By default the linked file will be visible under ``paket-files`` folder in project.