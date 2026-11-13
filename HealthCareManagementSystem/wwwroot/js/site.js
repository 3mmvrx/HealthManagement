// Small helpers shared by every page.
(function () {
    'use strict';

    // Show the toasts rendered by Views/Shared/_Notifications.cshtml
    if (window.bootstrap) {
        document.querySelectorAll('[data-hc-toast]').forEach(function (element) {
            new bootstrap.Toast(element, { delay: 6000 }).show();
        });
    }

    // <form data-confirm="Are you sure?"> asks before it is submitted.
    document.addEventListener('submit', function (event) {
        if (event.defaultPrevented) {
            return;
        }

        var form = event.target;
        var message = form && form.getAttribute ? form.getAttribute('data-confirm') : null;

        if (message && !window.confirm(message)) {
            event.preventDefault();
        }
    });

    // <input type="file" data-preview="#imageId"> previews the chosen picture.
    document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
        input.addEventListener('change', function () {
            var target = document.querySelector(input.getAttribute('data-preview'));

            if (target && input.files && input.files[0]) {
                target.src = URL.createObjectURL(input.files[0]);
            }
        });
    });
})();
