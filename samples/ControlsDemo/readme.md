# Estragonia – ControlsDemo sample

## About

A sample for [Estragonia](https://github.com/MrJul/Estragonia) which demonstrates a **separated project architecture** where the Avalonia UI lives in an independent class library, and the Godot project references it.

## Architecture

```
ControlsDemo/
├── ControlsDemo.sln
├── ControlsDemo.UI/           ← Avalonia class library (no Godot dependency)
│   ├── App.axaml
│   ├── Views/
│   │   └── MainView.axaml     ← Controls gallery (UserControl)
│   └── ...
└── ControlsDemo.Godot/        ← Godot host project
    ├── project.godot
    ├── AvaloniaLoader.cs      ← Bootstraps Avalonia
    ├── UserInterface.cs       ← Bridges Godot → Avalonia
    └── main.tscn
```

## Features

- **Separated projects**: Avalonia UI can be developed and tested independently of Godot
- **Controls showcase**: TextBox, ComboBox, CheckBox, RadioButton, Slider, ProgressBar, Button, ListBox
- **Dark FluentTheme**: Modern dark UI using Avalonia's Fluent theme
- **Card layout**: Responsive scrollable layout with card sections
- **Based on Avalonia 12.0.2**

## License

The whole Estragonia project source code, including this sample, is under the [MIT License](https://github.com/MrJul/Estragonia/blob/main/license.txt).
