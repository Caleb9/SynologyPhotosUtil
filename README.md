# Synology Photos Album List

List folders containing photos in a Synology Photos album.

## Why?

This is a simple console app querying Synology Photos API to deduce
locations (folder paths) of photos added to a Synology Photos *album*.

I used it while doing some spring cleaning of photos on my Synology
DiskStation NAS. Maybe someone will find it handy as well.

## How?

Code is targetting in .NET 6, and currently I don't plan to build
binaries for it. That means that **you need to have [.NET 6
SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) installed
to compile and run the app**.

Assuming that's done, clone the repository and in
`{repository_root}/SynologyPhotosAlbumList` simply do

```dotnet run```

This will display a help message about required command line
parameters, which are the following (in this order):

* Album name as it stands in Synology Photos (note: user needs to be
  the owner of this album)
* URL address of Synology Disk Station NAS
* Synology NAS user account name
* User's password
* Optional OTP code if user account has 2FA enabled

See the usage example below.

## Usage example

Assuming your Synology NAS user account is named "my_user", and you
are an *owner* of a Synology Photos album "My Album", you can run the
application in a following way:

```
dotnet run http://diskstation.address "My Album" my_user my_password
```

Alternatively, if your account has two factor authentication enabled,
you must also provide a one time code from your authenticator app as
the last argument e.g.:

```
dotnet run http://diskstation.address "My Album" my_user my_password 123456
```

Depending on your connection speed, how many albums you have and how
many photos are added to "My Album", querying the API can take a
moment. The output can for example look like this:

```
/private_space_folder/my_photos/IMG_1111.jpg
/shared_space_folder/IMG_2222.JPG
ERROR: IMG_3333.jpeg folder inaccessible
```

In this case "My Album" contains 3 photos:
* The the IMG\_1111.jpg photo is located in one of the user's "private
  space" folders, i.e. (by default) under `{your user's home
  directory}/Photos` in a `private_space_folder/my_photos` folder.
* The IMG\_2222.JPG photo is located in "shared space", i.e. under
  `photo` shared folder in `shared_space_folder` (note: the
  distinction between shared and private folders in the output is a
  TODO for now - for my own use case the difference is obvious thanks
  to the folder structure).
* In case of IMG\_3333.jpeg the physical location of the file is
  inaccessible for my\_user. This happens e.g. when there are other
  NAS users having access to "My Album" and they added photos from
  their *private folders* to which my\_user does not have
  access. These photos are listed at the end of the output.


## TODO

* Query albums that user is not owner of, but has access to
* Indicate if folder is private or shared in the output
* Improve command line syntax

## Code disclaimer

This is my first attempt at programming in F#. I'm coming from C#
world and I used this project as a learning exercise. The code could
probably be written in more functional way but I'm still learning. I'm
very open for feedback in this regard.


## Credits

* [zeichensatz/SynologyPhotosAPI](https://github.com/zeichensatz/SynologyPhotosAPI)
  contains description of the Synology Photos API that got me started
* [fsharpforfunandprofit.com](https://fsharpforfunandprofit.com) is a
  fantastic resource where I learned the F# basics
