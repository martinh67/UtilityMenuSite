# UtilityMenu Landing Page

A modern, responsive Blazor Server landing page for UtilityMenu - a professional C# Excel XLL add-in.

## Project Structure

```
UtilityMenuSite/
├── Components/          # Reusable UI components
│   ├── FeatureCard.razor
│   └── Footer.razor
├── Layout/             # Layout components
│   └── MainLayout.razor
├── Pages/              # Page components
│   ├── _Host.cshtml    # Entry point for Blazor Server
│   └── Home.razor      # Main landing page
├── Services/           # Business logic services (placeholder)
├── wwwroot/            # Static files
│   ├── css/
│   │   └── site.css    # Global styles
│   └── images/         # Image assets
├── Program.cs          # Application entry point
├── App.razor           # Root component
└── Routes.razor        # Routing configuration
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or .NET 8+)
- A code editor (Visual Studio, VS Code, or JetBrains Rider)

## Running the Application

### Option 1: Using the Command Line

```bash
dotnet run
```

The application will start and be available at:
- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001

### Option 2: Using Visual Studio

1. Open the project folder in Visual Studio
2. Press F5 or click the "Run" button

### Option 3: Using VS Code

1. Open the project folder in VS Code
2. Press F5 or use the terminal to run `dotnet run`

## Customizing the Site

### 1. Hero Section
Location: `Pages/Home.razor` (lines 8-28)
- Update the title, subtitle, and call-to-action text
- Replace the placeholder icon with an actual screenshot or image

### 2. Features Section
Location: `Pages/Home.razor` (lines 31-60)
- Modify the feature cards by editing the `FeatureCard` components
- Add or remove features as needed
- Change icons (currently using emoji, can replace with Font Awesome or custom images)

### 3. Call-to-Action Section
Location: `Pages/Home.razor` (lines 63-76)
- Update the download links
- Modify system requirements
- Change button text and links

### 4. Footer
Location: `Components/Footer.razor`
- Update company information
- Add or remove footer links
- Modify footer sections

### 5. Colors and Styling
- **Global styles**: `wwwroot/css/site.css`
- **Component-specific styles**: Each `.razor.css` file next to its component
- **Primary color**: Change `#2563eb` (blue) to your brand color
- **Gradient**: Modify gradient colors in `Pages/Home.razor.css`

### 6. Navigation Bar
Location: `Layout/MainLayout.razor`
- Add or remove navigation links
- Update the logo text
- Add a logo image

### 7. SEO Metadata
Location: `Pages/_Host.cshtml`
- Update the page title (line 10)
- Modify the meta description (line 9)
- Add additional meta tags for social media sharing

## Adding New Pages

1. Create a new `.razor` file in the `Pages` folder
2. Add the `@page` directive with the route (e.g., `@page "/pricing"`)
3. Use `@layout MainLayout` to apply the main layout
4. Add navigation links in `Layout/MainLayout.razor`

Example:

```razor
@page "/pricing"
@layout MainLayout

<PageTitle>Pricing - UtilityMenu</PageTitle>

<div class="container">
    <h1>Pricing Plans</h1>
    <!-- Your content here -->
</div>
```

## Adding Images

1. Place images in `wwwroot/images/`
2. Reference them using relative paths: `/images/your-image.png`

Example in Razor:
```html
<img src="/images/logo.png" alt="UtilityMenu Logo" />
```

## Deployment

### Deploy to Azure App Service

```bash
dotnet publish -c Release
# Then deploy the contents of bin/Release/net8.0/publish/ to Azure
```

### Deploy to IIS

1. Publish the application: `dotnet publish -c Release`
2. Copy the published files to your IIS server
3. Configure IIS to host the application

## Technology Stack

- **.NET 10**: Latest .NET version
- **Blazor Server**: Real-time web apps with C#
- **CSS**: Custom responsive design with CSS Grid and Flexbox

## Next Steps

- Add contact form functionality
- Integrate with a CMS for content management
- Add blog/news section
- Implement analytics tracking
- Add multilingual support

## License

Copyright © 2026 UtilityMenu. All rights reserved.
