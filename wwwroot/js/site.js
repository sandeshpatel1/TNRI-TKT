// Tanariri Musical Museum — client behaviour (session pill, OTP box, slots, ticket stepper)

(function sessionPill() {
  var pill = document.getElementById('pill');
  if (!pill) return;
  var num = document.getElementById('pillNum');
  var left = parseInt(pill.dataset.seconds, 10) || 600;
  var key = 'tanariri-session-deadline';
  var deadline = parseInt(sessionStorage.getItem(key), 10);
  if (!deadline || deadline < Date.now()) { deadline = Date.now() + left * 1000; sessionStorage.setItem(key, deadline); }

  function tick() {
    var rem = Math.max(0, Math.round((deadline - Date.now()) / 1000));
    var mm = String(Math.floor(rem / 60)).padStart(2, '0');
    var ss = String(rem % 60).padStart(2, '0');
    num.textContent = mm + ':' + ss;
    pill.classList.toggle('urgent', rem <= 60);
    if (rem <= 0) { clearInterval(t); window.location.href = '/'; }
  }
  tick();
  var t = setInterval(tick, 1000);
})();

// Mobile / OTP: digits only
document.addEventListener('input', function (e) {
  if (e.target.matches('.otp-single, input[inputmode="numeric"]')) {
    e.target.value = e.target.value.replace(/\D/g, '');
  }
});

// ---------------- Plan (date + slot picker) ----------------
function initPlan(slotsUrl) {
  var dateInput = document.getElementById('dateInput');
  var grid = document.getElementById('slotGrid');
  var msg = document.getElementById('slotMsg');
  var slotInput = document.getElementById('slotInput');
  var summary = document.getElementById('planSummary');
  var sumText = document.getElementById('sumText');
  if (!dateInput) return;

  function render(data) {
    grid.innerHTML = '';
    if (data.closed) { msg.textContent = data.message; summary.hidden = true; return; }
    if (!data.slots.length) { msg.textContent = 'No slots configured.'; return; }
    msg.textContent = 'Tap a time slot below.';
    data.slots.forEach(function (s) {
      var el = document.createElement('button');
      el.type = 'button';
      el.className = 'slot' + (s.disabled ? ' out' : s.left <= 2 ? ' low' : '');
      el.textContent = s.time;
      el.disabled = s.disabled;
      el.addEventListener('click', function () {
        grid.querySelectorAll('.slot').forEach(function (x) { x.classList.remove('on'); });
        el.classList.add('on');
        slotInput.value = s.time;
        sumText.textContent = dateInput.value + ' at ' + s.time + ' (' + s.left + ' left)';
        summary.hidden = false;
      });
      grid.appendChild(el);
    });
  }

  function load() {
    if (!dateInput.value) return;
    slotInput.value = ''; summary.hidden = true;
    msg.textContent = 'Loading available slots…';
    fetch(slotsUrl + '?date=' + encodeURIComponent(dateInput.value), { headers: { 'X-Requested-With': 'fetch' } })
      .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
      .then(render)
      .catch(function () { msg.textContent = 'Could not load slots. Please try again.'; });
  }
  dateInput.addEventListener('change', load);
  if (dateInput.value) load();
}

// ---------------- Ticket selection ----------------
function initTickets() {
  var form = document.getElementById('ticketForm');
  if (!form) return;
  var leftEl = document.getElementById('left');
  var payBtn = document.getElementById('payBtn');
  var linesEl = document.getElementById('lines');
  var sumPeople = document.getElementById('sumPeople');
  var sumTotal = document.getElementById('sumTotal');
  var maxLeft = parseInt(leftEl.dataset.left, 10) || 0;

  function rows() { return Array.from(form.querySelectorAll('.tk')); }

  function recalc() {
    var people = 0, total = 0, html = '';
    rows().forEach(function (row) {
      var input = row.querySelector('input');
      var q = parseInt(input.value, 10) || 0;
      var price = parseFloat(row.dataset.price) || 0;
      people += q; total += q * price;
      row.classList.toggle('has', q > 0);
      if (q > 0) html += '<div><span>' + row.dataset.name + ' &times; ' + q + '</span><span>' + (price === 0 ? 'Free' : '₹' + (q * price).toFixed(0)) + '</span></div>';
    });
    linesEl.innerHTML = html || '<em class="muted">No tickets selected yet</em>';
    sumPeople.textContent = people;
    sumTotal.textContent = total.toFixed(0);
    var remaining = maxLeft - people;
    leftEl.textContent = Math.max(0, remaining);
    rows().forEach(function (row) {
      var plus = row.querySelector('.plus');
      plus.disabled = remaining <= 0;
    });
    payBtn.disabled = people === 0 || people > maxLeft;
  }

  form.addEventListener('click', function (e) {
    var btn = e.target.closest('.minus, .plus');
    if (!btn) return;
    var input = btn.parentElement.querySelector('input');
    var v = parseInt(input.value, 10) || 0;
    if (btn.classList.contains('plus')) v += 1; else v = Math.max(0, v - 1);
    input.value = v;
    recalc();
  });

  recalc();
}
