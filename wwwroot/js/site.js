// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Validate date inputs to prevent years beyond 4 digits
document.addEventListener('DOMContentLoaded', function() {
    // Apply validation to all date and datetime-local inputs
    const dateInputs = document.querySelectorAll('input[type="date"], input[type="datetime-local"]');
    
    dateInputs.forEach(function(input) {
        input.addEventListener('input', function() {
            validateDateYear(this);
        });
        
        input.addEventListener('change', function() {
            validateDateYear(this);
        });
    });
    
    function validateDateYear(input) {
        const value = input.value;
        if (!value) return;
        
        // Extract year from date (yyyy-MM-dd) or datetime-local (yyyy-MM-ddTHH:mm)
        const yearMatch = value.match(/^(\d+)/);
        if (yearMatch && yearMatch[1].length > 4) {
            // Truncate year to 4 digits
            const truncatedYear = yearMatch[1].slice(0, 4);
            input.value = value.replace(/^\d+/, truncatedYear);
        }
    }
});
