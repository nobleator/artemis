# Background
F# + Fable + Elmish + Feliz + Vite + Preact

Fable converts the F# code to JavaScript. Vite serves that JavaScript.

F# -> compiled to JavaScript by Fable -> managed with Elmish -> UI built with Feliz -> rendered by Preact -> bundled/served by Vite

This folder is expected to contain a `/public/` folder with an `index.html` file with key ID & script references to the generate `Program.js` file. It also contains `package.json` and `vite.config.js`.

There is a sub-folder for the source code, `/src/` with 2 subfolders, `/app/`, which contains the application code, and `/test/`, which contains unit tests.

# Setup
Install dotnet fable tool:
`cd src`
`dotnet new tool-manifest`
`dotnet tool install fable`
Install vite globally with `pnpm i -g vite`
Install npm packages with `pnpm i`

# Unit Testing
TODO
From `/src/test/`, run `dotnet watch test`

# Dev
From `/src/app/`, run `dotnet fable watch --outDir ../../public`
From parent folder, run `pnpm run dev`

# Deploy
From the root folder run `pnpm run build`, which performs the following steps:
- Compile Fable from `/artemis/src/app/`, run `dotnet fable --outDir ../../public`.
- From root (`/artemis/`) run: `vite build`.  This will generate `/artemis/public/dist/`.
- Copy over leaflet images from `/artemis/public/leaflet/images/` to `/artemis/public/dist/`.
- Copy over data files from `/WebClieartemisnt/public/data/` to `/artemis/public/dist/data/`.
This build output can then be deployed however you like, such as drag & drop the entire `/artemis/public/dist/` folder on Netlify.