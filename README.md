# APak

Simple Packing Algorithm optimized to bundle Resources together

## Usage

The main Usage of this Algorithm lies in the bundling of embedded Resources together.
Instead of adding many Resources to an Application, all Files can be packed into a single Resource.
It's also usable whenever Data can only be processed in a single use Stream that can't be seeked, for example network Streams.
The Simplicity requires almost no Memory and no complex Operations with Compression being completely optional.

Using APak in your code is very simple:

### Packing Files

	using (var FS = File.Create(@"C:\TestFile.pak"))
	{
		APak.Pack(APak.GetFiles(@"C:\Test"), FS);
	}

The Method `APak.GetFiles` builds the Directory Listing for a Pak File. Arguments:

1. The Root Directory to scan. String

The Method `APak.Pack` packs the Files together. Arguments:

1. File Listing from `APak.GetFiles`. IEnumerable<FileSpec>
2. Stream to write to. Stream
3. (opt) Compression Algorithm. Compression; Defaults to `Compression.AllCompression`
4. (opt) Log Output. TextWriter; Defaults to `null`

### Unpacking Files

	using (var FS = File.OpenRead(@"C:\TestFile.pak"))
	{
		APak.Unpack(FS, @"C:\PakOutput");
	}

The Method `APak.Unpack` extracts all Files and Directories from a Pak File. Arguments:

1. Reader positioned at the Start of a pak Header. Stream
2. (opt) Output Directory. String; If not specified, the Stream is scanned for all Entries but nothing is extracted
3. (opt) Collect File Entries. Bool; Defaults to `false`. The Function will return a List of all Entries if set to `true`
4. (opt) Log Output. TextWriter; Defaults to `null`

## Usage examples

Just a few usage Examples

- Providing sets of Defaults for a User he can chose from
- Compressing Languages together and extracting the one the User selects
- Including DLL Files to publish a single exe only, not a zip File with many Files in it that can get lost
- Sending multiple Files from one Command to another via Standard Input and Output

## Features

Explanation of the most distict Features

### Embeddable

This Format can be embedded in other Formats.
It has a Header that can be detected and a known Length of the File List,
which allows Content to appear before the File and after the File.

For the default Implementation to work you have to seek to the Header yourself

### Large file support

Supports Files up to 8 EiB (2^63)

### Large Directory Support

Supports Directories of up to 2 billion entries (2^31)

### Simplicity

The Algorithm and File Format is very simple to understand and implement.
This makes implementing your own Packer or porting this Packer into other Languages very easy. Fields are made up of very common Data Types and are always in the same Order, making for very simple Parser.

### Streamable

Packing and unpacking doesn't requires the base Stream to be seekable.
If individual Compression is used when packing,
a temporary File or some Memory has to be used for unseekable output Streams to calculate the Length.
Unpacking never requires seeking of the input Stream

### Empty Directories

The Algorithm supports creating empty Directory structures.
It's not necessary to put 0-byte Files into Directories to pack them.

### Compression

The Format supports optional Compression using GZip.
Below is an explanation of all supported Compression Types.

#### No Compression

The Data is not compressed at all.
This Mode results in the biggest resource File but is almost always the fastest to pack and unpack.
It's only slower than Compression if large Files are packed that are very well compressible.

This Mode is recommended if the Files are already in a compressed Format like mp3, 7z or zip Files.
This Mode can be selectively applied to individual Files in the pak File

#### Individual Compression

Compresses individual Files.
This leaves the Directory Structure uncompressed.
This Mode still allows to quickly seek between Entries.
This Mode can be selectively applied to individual Files in the pak File.

#### All Compression

Compresses the entire pak File.
This Mode is independent of the other compression Method.
Individual Files in the pak File (allthough possible) should not be individually compressed when using this Mode.
Double compression almost never results in a smaller File but will double the GZip Speed Penalty.

## File Format

All numbers are in BigEndian Format.
Numbers are treated as signed even where it makes no Sense technically,
"Number of entries" for example. This makes porting to Languages and Systems with no unsigned Number Support easier (for Example Java).
Length prefixed Strings are encoded in UTF-8.
The Prefix always specifies the number of Bytes and not the number of Characters/Codepoints since these can differ for UTF-8.
The basic File Format of a pak File is as follows:

### Header

| Size (in Bytes) | Value                    |
| --------------- | ------------------------ |
| 4               | `APAK`                   |
| 1               | Compression              |
| 4               | Number of Entries        |
| Dynamic         | Entry[Number of Entries] |

#### APAK

This is the literal ASCII String `APAK` (in bytes: `41 50 41 4B`).
This should always be read as 4 Bytes and never interpreted as UTF-8 or any other variable Length or multibyte Encoding.

#### Compression

This specifies the Compression Algorithm used.

Compression Flags can have these Values:

| Value | Meaning                |
| ------| ---------------------- |
| 0     | No Compression         |
| 1     | Individual Compression |
| 2     | All GZip Compression   |

Value `1` is not valid in the Header and is internally used to compress all Files but individually.
Any Value not specified in the Table is invalid and the Unpacker should stop.
This allows for later extension with other compression Algorithms

If this Value is `2`, everything that follows the "Number of Entries" Field is compressed using a single GZip Stream.

#### Number of Entries

32 bit Integer specifying the Number of Entries in the List that follows

### Entry

Entries are structured like this:

| Size (in bytes) | Value                    |
| --------------- | ------------------------ |
| 1               | Entry Type               |
| 4               | Name Length              |
| Name Length     | Entry Name               |
| 8               | File Size    (File only) |
| File Size       | Content      (File only) |

#### Entry Type

This is a Bitfield of one or more of these Values

| Value | Meaning         |
| ------| --------------- |
| 1     | Directory       |
| 2     | File            |
| 4     | GZip Compressed |

All allowed Combinations and how to interpret them:

- 1: File System Directory
- 2: Uncompressed File
- 6: Compressed File using GZip Algorithm

Any unspecified Combination in the List above is considered invalid to allow for further Expansion later.

#### Name Length

This specifies the File Name Length in Bytes

#### Entry Name

This is the Name of the entry, as long as the "Name Length" Field specifies.
The Entry uses `/` as Directory Separator and must not start with `/`.

An Unpacker should check for Attempts to break out of the specified Output Directory using Entry Name Segments (for Example `../`).

#### File Size

This Field is only available for Files, not Directories.
It tells how many of the following Bytes are for this File Entry.
This is always the raw Number of Bytes that need to be read from the Stream regardless of Compression.

#### File Content

This Field is only available for Files, not Directories.
This is the Content of the File.
If the File is compressed, the Bytes need to be decompressed using the proper Algorithm before the File is written to Disk. What Algorithm has to be used is defined by the Entry Type Bitfield. Currently only GZip or uncompressed Content are defined.

## Directory Entries

Directory Entries are not necessary unless an empty Directory is packed.
Directories that do not exist are created for Files.

### Missing Fields

Directory Entries lack the "File Size" and "File Content" Fields. They are followed directly by the next Entry if not the last one.

# Command Line Utility "Test.exe"

The Library comes with a simple Command line Utility to pack and unpack files.
It can be used by Developers to test the Packer and pack Files.
While fully usable and able to unpack Files it's recommended that you implement your own more friendly Utility, especially when exposing it to Users.

Use `/?` to get a Help on the Arguments.

## Compression Mixing

Individual Files in a pak File can be compressed,
this allows to chose the optimal value on an individual File Base.
You can for example compress Text and Configuration Files but not PNG Files since they are already compressed.
The default Implementation provided in this Library can't do this.
It will correctly unpack a mixed pak File.
If you plan on adding mixed Compression support I suggest you add a Property to the `APak.FileSpec` Class to define individual Compression.

## Exception Handling

This Library is meant for packing and unpacking trusted Data only.
It doesn't necessarily catches all possible Attempts to break out of the Directory or supplying invalid Data.
