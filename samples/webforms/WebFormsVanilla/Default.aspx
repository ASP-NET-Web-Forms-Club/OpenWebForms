<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="WebFormsVanilla.Default" %>
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Book Library — Vanilla</title>
    <link rel="stylesheet" href="css/site.css" />
</head>
<body>
    <header class="app-header">
        <div class="inner">
            <h1>📚 Book Library</h1>
            <span class="tag">Vanilla (API + Fetch)</span>
        </div>
    </header>

    <div class="container">
        <div class="two-col">

            <!-- Left card: Add / Edit form (plain HTML inputs) -->
            <div class="card">
                <h2>Add / Edit Book</h2>

                <div id="msg"></div>

                <div id="book-form">
                    <input type="hidden" id="book-id" value="" />

                    <div class="field">
                        <label for="title">Title</label>
                        <input type="text" id="title" />
                    </div>

                    <div class="field">
                        <label for="author">Author</label>
                        <input type="text" id="author" />
                    </div>

                    <div class="field">
                        <label for="genre">Genre</label>
                        <select id="genre">
                            <option value="Fiction">Fiction</option>
                            <option value="Non-Fiction">Non-Fiction</option>
                            <option value="Sci-Fi">Sci-Fi</option>
                            <option value="Fantasy">Fantasy</option>
                            <option value="Biography">Biography</option>
                            <option value="Mystery">Mystery</option>
                        </select>
                    </div>

                    <div class="field">
                        <label for="year">Year</label>
                        <input type="number" id="year" />
                    </div>

                    <div class="field inline-check">
                        <label><input type="checkbox" id="in_stock" /> In&nbsp;Stock</label>
                    </div>

                    <div class="btn-row">
                        <button type="button" onclick="saveBook()">Save</button>
                        <button type="button" class="btn-secondary" onclick="clearForm()">Clear</button>
                    </div>
                </div>
            </div>

            <!-- Right card: list filled by JS from the server-rendered fragment -->
            <div class="card">
                <h2>Books</h2>
                <div id="book-list"></div>
            </div>

        </div>
    </div>

    <script src="js/books.js"></script>
</body>
</html>
