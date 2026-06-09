# Third-Party Notices

Radoub reimplements (does not vendor) behavior derived from the following projects.
Code was studied and ported to C#; original source is not bundled. See each project's
repository for full license text.

## Aurora MDL Particle Emitter Rendering (#2395)

The model-preview particle/emitter rendering in `Radoub.UI` was derived from:

- **rollnw** — https://github.com/jd28/rollnw — MIT License, Copyright (c) jmd.
  Primary reference for the particle runtime definition, the MDL emitter compile model,
  and the Aurora particle simulation (spawn / cone emission / integration / over-life curves).

- **nwn_mdl_webviewer** — https://github.com/dunahan/nwn_mdl_webviewer — MIT License.
  Reference for Aurora emitter constants (gravity ≈ 9.81·mass, exponential drag, cone spread)
  and the `c_allip_d.mdl` test fixture.

- **nwnexplorer** — https://github.com/virusman/nwnexplorer — BSD License.
  Reference for the binary MDL emitter node layout and the emitter controller type IDs.

## Aurora File Format Parsing

- **neverwinter.nim** — https://github.com/niv/neverwinter.nim — MIT License.
  Primary reference for Aurora file-format parsing across the toolset.
