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

```dotnet run -- --help```

This will display a help message about required command line
arguments:

```
Global options:
    -h, --help                  Prints this message

Commands:
    list <ALBUM-NAME>           List photos in album
    
Common command options (available for all commands):
    -a, --address <URL>         [REQUIRED] HTTP(S) address of Synology DSM
    -u, --user <USER-NAME>      [REQUIRED] DSM user account name
    -p, --password <PASSWORD>   [REQUIRED] DSM user account password
    -o, --otp <OTP-CODE>        OTP code when 2FA is enabled for user account
```

* ALBUM-NAME as it stands in Synology Photos (note: user needs to be
  the owner of this album)

See the usage example below.


## Usage example

Assuming your Synology NAS user account is named "my_user", and you
are an *owner* of a Synology Photos album "My Album", you can run the
application in a following way:

```
dotnet run -- list "My Album" -a http://diskstation.address -u my_user -p my_password
```

**Note**: The address value should be the same as the one you use to
open DSM in your browser. Unless you use non-standard ports (5000 for
HTTP and 5001 for HTTPS), you can omit the port, otherwise it needs to
be specified e.g. http://diskstation.address:5042.

Alternatively, if your account has two factor authentication enabled,
you must also provide a one time code from your authenticator app as
the last argument e.g.:

```
dotnet run -- list "My Album" -a http://diskstation.address -u my_user -p my_password -o 123456
```

Depending on your connection speed, how many albums you have and how
many photos are added to "My Album", querying the API can take a
moment. The output can for example look like this:

```
P: /personal_space_folder/my_photos/IMG_1111.jpg
S: /shared_space_folder/IMG_2222.JPG
ERROR: IMG_3333.jpeg folder inaccessible
```

In this case "My Album" contains 3 photos:
* The the IMG\_1111.jpg photo is located in one of the user's
  "personal space" folders (indicated by leading "P"), i.e. (by
  default) under `{your user's home directory}/Photos` in a
  `personal_space_folder/my_photos` folder.
* The IMG\_2222.JPG photo is located in "shared space" (indicated by
  leading "S"), i.e. under `photo` shared folder in
  `shared_space_folder`.
* In case of IMG\_3333.jpeg the physical location of the file is
  inaccessible for my\_user. This happens e.g. when there are other
  NAS users having *provider* access to "My Album" and they added
  photos from folders to which my\_user does not have access. These
  photos are listed at the end of the output.


## TODO

* Query albums that user is not owner of, but has access to
* Add add-photo-to-album command
* Add optional debug information
* Add export to Synology Photos folder command


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
