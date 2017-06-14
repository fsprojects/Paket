## Silent mode

The silent mode can be used for tooling support in various editors.
It allows to create suggestions for [`paket add`](paket-add.html):

```sh
paket find-packages --silent
```

The command prints package names matching entered text enabling package name
suggestions. It will keep running in a loop until it receives the text `:q`.

## Example

Run the command `paket find-packages --silent` and enter a search term (`fake`
was used here).

```sh
$ paket find-packages --silent
fake
FAKE
FakeSign
Faker
FakeO
FakeHost
FakeData
FAKEX
FAKE.SQL
FAKE.IIS
FAKE.Lib
Fake.AWS
FakeN.Web
FSharp.FakeTargets
FakeHttp
FakeDb
FakeDbSet
FAKE.Core
FAKE.Gallio
Faker.Net
Sitecore.FakeDb
```
