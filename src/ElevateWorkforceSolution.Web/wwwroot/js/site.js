/* ==========================================================================
   Elevate Workforce Solution - interaction layer
   GSAP animations are opt-in and always respect prefers-reduced-motion.
   ========================================================================== */
(function () {
  'use strict';

  var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  var motionAllowed = !reduceMotion && typeof window.gsap !== 'undefined';

  /* ---------- Mobile navigation ---------- */

  function initNav() {
    var toggle = document.querySelector('[data-nav-toggle]');
    var drawer = document.getElementById('nav-drawer');
    if (!toggle || !drawer) return;

    toggle.addEventListener('click', function () {
      var open = drawer.classList.toggle('open');
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
      toggle.querySelector('[data-nav-label]').textContent = open ? 'Close' : 'Menu';
    });

    // Close the drawer when focus or a click lands outside of it.
    document.addEventListener('click', function (e) {
      if (!drawer.classList.contains('open')) return;
      if (drawer.contains(e.target) || toggle.contains(e.target)) return;
      drawer.classList.remove('open');
      toggle.setAttribute('aria-expanded', 'false');
      toggle.querySelector('[data-nav-label]').textContent = 'Menu';
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && drawer.classList.contains('open')) {
        drawer.classList.remove('open');
        toggle.setAttribute('aria-expanded', 'false');
        toggle.querySelector('[data-nav-label]').textContent = 'Menu';
        toggle.focus();
      }
    });
  }

  /* ---------- Confirm before destructive submits ---------- */

  function initConfirms() {
    document.addEventListener('submit', function (e) {
      var form = e.target;
      var message = form.getAttribute('data-confirm');
      if (message && !window.confirm(message)) {
        e.preventDefault();
      }
    });
  }

  /* ---------- Auto-submit filter forms on change ---------- */

  function initAutoSubmit() {
    document.querySelectorAll('[data-auto-submit]').forEach(function (field) {
      field.addEventListener('change', function () {
        var form = field.closest('form');
        if (form) {
          var page = form.querySelector('input[name="page"]');
          if (page) page.value = '1';
          form.submit();
        }
      });
    });
  }

  /* ---------- Animate progress bars and meters ---------- */

  function initMeters() {
    document.querySelectorAll('[data-meter-fill]').forEach(function (el) {
      var target = parseFloat(el.getAttribute('data-meter-fill') || '0');
      var pct = Math.max(0, Math.min(100, target));

      if (!motionAllowed) {
        el.style.width = pct + '%';
        return;
      }

      window.gsap.from(el, { width: '0%', duration: 0.9, ease: 'power2.out', delay: 0.1 });
    });
  }

  /* ---------- Animate chart bars ---------- */

  function initBars() {
    document.querySelectorAll('[data-bar-fill]').forEach(function (el) {
      var target = parseFloat(el.getAttribute('data-bar-fill') || '0');
      var pct = Math.max(0, Math.min(100, target));

      if (!motionAllowed) {
        el.style.width = pct + '%';
        return;
      }

      window.gsap.from(el, { width: '0%', duration: 0.7, ease: 'power2.out', stagger: 0.05 });
    });
  }

  /* ---------- Count-up statistics ---------- */

  function initCounters() {
    var counters = document.querySelectorAll('[data-count-to]');
    if (!counters.length) return;

    counters.forEach(function (el) {
      var target = parseFloat(el.getAttribute('data-count-to') || '0');

      if (!motionAllowed) {
        el.textContent = target.toLocaleString();
        return;
      }

      var state = { value: 0 };
      window.gsap.to(state, {
        value: target,
        duration: 1.2,
        ease: 'power2.out',
        onUpdate: function () { el.textContent = Math.round(state.value).toLocaleString(); }
      });
    });
  }

  /* ---------- Scroll reveal ---------- */

  function initReveal() {
    var items = document.querySelectorAll('.reveal');
    if (!items.length) return;

    if (!motionAllowed || !('IntersectionObserver' in window)) {
      items.forEach(function (el) { el.classList.add('is-visible'); });
      return;
    }

    document.documentElement.classList.add('js-anim');

    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        var el = entry.target;
        window.gsap.to(el, {
          opacity: 1,
          y: 0,
          duration: 0.55,
          ease: 'power2.out',
          delay: parseFloat(el.getAttribute('data-reveal-delay') || '0')
        });
        observer.unobserve(el);
      });
    }, { threshold: 0.12, rootMargin: '0px 0px -40px 0px' });

    items.forEach(function (el) {
      window.gsap.set(el, { opacity: 0, y: 18 });
      observer.observe(el);
    });
  }

  /* ---------- Hero entrance ---------- */

  function initHero() {
    var hero = document.querySelector('[data-hero]');
    if (!hero || !motionAllowed) return;

    var targets = hero.querySelectorAll('[data-hero-item]');
    window.gsap.from(targets, {
      opacity: 0,
      y: 26,
      duration: 0.7,
      ease: 'power2.out',
      stagger: 0.09,
      delay: 0.1
    });
  }

  /* ---------- Filter chips: reflect active state in the URL bar ---------- */

  function initFilterChips() {
    document.querySelectorAll('[data-filter-chip]').forEach(function (chip) {
      if (chip.getAttribute('href') && chip.classList.contains('active')) {
        chip.setAttribute('aria-current', 'true');
      }
    });
  }

  /* ---------- Password strength hint ---------- */

  function initPasswordHint() {
    var input = document.getElementById('Password');
    var hint = document.getElementById('password-hint');
    if (!input || !hint) return;

    input.addEventListener('input', function () {
      var v = input.value;
      var checks = [v.length >= 8, /[a-z]/.test(v), /[A-Z]/.test(v), /\d/.test(v), /[^A-Za-z0-9]/.test(v)];
      var met = checks.filter(Boolean).length;
      hint.textContent = 'Password strength: ' + met + ' of 5 requirements met';
      hint.className = 'form-text ' + (met === 5 ? 'fw-700' : 'text-muted');
    });
  }

  /* ---------- Boot ---------- */

  function boot() {
    initNav();
    initConfirms();
    initAutoSubmit();
    initMeters();
    initBars();
    initCounters();
    initReveal();
    initHero();
    initFilterChips();
    initPasswordHint();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})();
