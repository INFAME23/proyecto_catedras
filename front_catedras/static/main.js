document.addEventListener('DOMContentLoaded', function() {
    console.log('Catedras USB - Aplicacion cargada');
    
    document.querySelectorAll('.alert').forEach(alert => {
        setTimeout(() => {
            alert.style.transition = 'opacity 0.5s ease';
            alert.style.opacity = '0';
            setTimeout(() => alert.remove(), 500);
        }, 5000);
    });
});
