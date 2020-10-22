# Testimonials and Quotes

## GROSSWEBER

> Paket solves dependency resolution very elegantly.

We at [GROSSWEBER](https://grossweber.com/en) are fans of command line tools
that serve a single purpose, like [git](https://grossweber.com/git). Paket is
such a tool that works extremely well when compared to other solutions available
on the .NET platform. We use Paket not only to manage dependencies, but also to
create our own NuGet packages. All in an easy-to-use, consistent, reliable and
fast way. Anybody who has been working with dependency managers on other
platforms like npm, bundler and pip will be pleased to find a similar workflow
with Paket.

We love what we do and with love comes passion. This passion drives us to
actively engage in our client projects and to design and develop as if they were
our own. Spreading the word about Paket in our
[trainings](http://grossweber.com/trainings) comes naturally.

[Alexander Groß](https://github.com/agross),
[GROSSWEBER](https://grossweber.com/en)

![GROSSWEBER likes Paket](img/testimonials-grossweber.png "GROSSWEBER likes Paket")

## 15below

> Paket has removed the pain from disciplined dependency management.

[15below](http://15below.com) started moving from copying binary dependencies
around to nuget dependencies several years ago, and it's been a massive help
with our internal coding discipline. But the time overheads added to development
by versioned path merge conflicts and the confusion caused by multiple versions
within solutions was becoming more and more of a pain point. It reached the
stage where we were considering moving back to a method of working we knew was
less efficient just to avoid the pain of the tooling.

Paket has removed the pain from disciplined dependency management. Sensible
design for large, shared projects protects us from insane merge conflicts and on
the few occasions that we have discovered bugs or missing features the clean
codebase and responsive maintainers have enabled us to fill those gaps rapidly -
often with a new release the same day.

Michael Newton (Technical Architect) [@mavnn](https://twitter.com/mavnn),
[15below](http://15below.com)

## Basware

> Paket is a tool which makes it easy to manage enterprise application
> dependencies and component upgrades.

[Basware](http://www.basware.com/) Purchase to Pay is a large scale enterprise
application for simplifying and streamlining key financial processes.

Managing the 3rd-party component dependencies over more than 100 solutions can
be challenging, however Paket handles this well, and guarantees that the
dll-libraries are all the correct version.

With over 80 third party components in use, Paket gives us a clear vision of the
dependency tree. This helps when making work estimates for major component
version upgrades. Before taking Paket into use, making work estimates for
component upgrades was painful, as the dependencies were not always clearly
understood.

Tuomas Hietanen (Lead Software Engineer) [Basware](http://www.basware.com/)

## Betgenius

> Paket has improved the productivity of our teams by reducing the dependency
> management overhead we had been experiencing with NuGet.

[Betgenius](http://www.betgenius.com) is a data provider to the sports betting
industry with peak load exceeding 100,000 messages/second. Our software
architecture is highly distributed to facilitate business demands for
scalability which means we are currently building around 150 .NET solutions.
Using an internal NuGet repository we share our internal and 3rd party
dependencies. Trying to achieve continuous integration with the out-of-the-box
NuGet client is next to impossible at this scale due to NuGet's clumsy version
management implementation.

Paket has enabled us to explicitly define our constraints and forces us to think
about how we want our dependencies to look. The Paket client is much more
intuitive. It is vastly quicker at performing updates than the NuGet client and
we don't have a constant stream of csproj updates to change the reference paths.
Another huge benefit we got from using Paket is that the paket pack command
creates a much better defined package than the NuGet pack. Paket inspects your
dependencies file and uses that to put strict constraints into the nuspec file
unlike the much less advanced NuGet client.

Chris Haines (Development Team Lead) [@cjbhaines](https://twitter.com/cjbhaines),
[Betgenius](http://www.betgenius.com)

## Nessos

> Paket is an excellent dependency management and package authoring tool that
> has greatly improved our productivity.

[Nessos](http://www.nessos.gr/) is an Athens, Greece based ISV and consultancy
focused on .NET/F# technologies with expertise in tailored business applications
and scalable computation. Nessos is a [major
contributor](https://github.com/nessos) in .NET open source software and is
author and primary contributor of the [MBrace](http://www.m-brace.net/)
distributed computation library.

Paket has enabled us to quickly and effectively control dependencies in large
and complicated projects we have been developing both in-house and in our open
source development. Its simple design philosophy is an excellent match to FAKE
and git and is ideal for cross platform development. Special mention needs to
made of paket pack: we heavily modularize our code in small nuget packages that
need to be updated often. The default nuget tooling for authoring packages was
cumbersome to use since dependency constraints needed to be edited by hand. This
often resulted in accidental releases of dud packages. Packet pack does away
with that concern; authoring a package update now takes less than 10 seconds of
manual work.

Eirik Tsarpalis (Nessos R&D)
[@eiriktsarpalis](https://twitter.com/eiriktsarpalis),
[Nessos](http://www.nessos.gr)


## Rhein-Spree Software Engineering GmbH

> Paket enabled us to share code between repositories and solutions without the need to create new packages every time something changed.

[Rhein-Spree](https://www.rhein-spree.com) is software services and consulting company from Berlin with teammates in Leipzig, Dresden and Düsseldorf. We are focused on .NET Technologies with great results.

When it comes to dependency management in growing environment with multiple solutions etc. we
felt the pain of inconsistent resolution of package versions across the repository. It got
even harder as the solutions grew. At some point we decided to give Paket a try and what should
we say... We had some work in getting the dependencies file correct and then it worked like a
charm. Even better. We also started to use the git functionality of Paket in referencing other
git repositories. This enabled us to share code between repositories and solutions without the
need to create new packages everytime something changed.

So the time we needed to resolves versioning issues has decreased tremendously. 

Great work, please keep it up!

[Sebastian Brandt](https://github.com/brase),
[Rhein-Spree](https://www.rhein-spree.com)

![Rhein-Spree likes Paket, too](img/testimonials-rheinspree.png "RheinSpree likes Paket, too")

## Please contribute your testimonials!

Adding new testimonials to this page is easy. Just write your quote in plain
text, using some Markdown formatting and send a pull request to
[docs/content/testimonials.md](https://github.com/fsprojects/Paket/blob/master/docs/content/testimonials.md).
Look at the existing examples to see how this works.
