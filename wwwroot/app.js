// ensure global apiBase exists without redeclaring identifier
if (typeof window.apiBase === 'undefined') {
  window.apiBase = location.origin + '/api';
}

// Ensure UTF-8 support in browser
if (document.characterSet !== 'UTF-8' && document.charset !== 'utf-8') {
  console.warn('Warning: Document is not UTF-8 encoded, may have issues with Cyrillic text');
}

function parseJwt(token){
  try{
    const part = token.split('.')[1];
    if(!part) return null;
    // handle URL-safe base64
    const safe = part.replace(/-/g,'+').replace(/_/g,'/');
    const padded = safe.padEnd(safe.length + (4 - safe.length % 4) % 4, '=');
    const json = atob(padded);
    return JSON.parse(json);
  }catch(e){ return null; }
}

// Safely handle UTF-8 strings and prevent character corruption
function safeText(str) {
  if (!str) return '';
  if (typeof str !== 'string') return String(str);
  try {
    // No need to replace characters - UTF-8 is properly handled now
    return str;
  } catch(e) {
    return String(str);
  }
}

const app = Vue.createApp({
  data() {
    return {
      token: localStorage.getItem('token') || '',
        // whether to persist token across sessions
        remember: false,
        // whether to show full token instead of truncated
        showFullToken: false,
        // track if token was persisted to storage
        tokenPersisted: !!localStorage.getItem('token'),
      // file upload helpers
      uploadEmployeeId: '',
      base64Content: '',
      base64FileName: '',
      // global files view
      allFiles: [],
      uploading: [],
      showAllFiles: false,
      auth: { username: '', password: '' },
        showSwagger: false,
      contracts: [],
      filters: { q: '', status: '', departmentId: '' },
      newContract: { title: '', contractNumber: '', departmentId: null, responsibleEmployeeId: null, amount: null, description: '', status: 'Draft', currency: 'RUB', contractType: '', startDate: '', endDate: '' },
      selectedContract: null,
      tags: [],
      files: [],
      history: [],
      tagToAdd: '',
      attachFileId: '',
      // pagination
      currentPage: 1,
      pageSize: 10,
      totalCount: 0
    };
  },

  computed: {

    tokenShort() { return this.token ? (this.showFullToken ? this.token : this.token.slice(0,40) + '...') : '(none)'; },
    isAdmin(){
      if(!this.token) return false;
      const p = parseJwt(this.token);
      if(!p) return false;
      // possible claim names
      if(p.role){ if(Array.isArray(p.role)) return p.role.includes('admin'); if(p.role==='admin') return true; }
      const roleClaim = p['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || p['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/role'] || p['role'];
      if(roleClaim){ if(Array.isArray(roleClaim)) return roleClaim.includes('admin'); return roleClaim === 'admin'; }
      // sometimes roles are in 'roles' or 'unique_name'
      if(p.roles){ if(Array.isArray(p.roles)) return p.roles.includes('admin'); }
      return false;
    },
    totalPages(){ return Math.max(1, Math.ceil(this.totalCount / this.pageSize)); }
  },
  methods: {
    // Format ISO date string to readable
    fmtDate(d){ if(!d) return ''; try{ const dt = new Date(d); return dt.toLocaleString(); }catch(e){ return d; } },
    // Translate status to Russian
    fmtStatus(status){ const map = { 'Draft': 'Draft', 'Active': 'Active', 'Expired': 'Expired' }; return map[status] || status; },
    onLoadContracts(){ try{ console.log('onLoadContracts clicked'); }catch(e){} this.loadContracts(); },
    // Open file picker for contracts CSV import
    onPickContractsCsv(){
      try{ console.log('onPickContractsCsv clicked'); }catch(_){}
      const el = document.getElementById('contractsCsvInput');
      if(el) el.click();
    },

    // CSV file selected handler
    async onCsvSelected(e){
      const input = e.target;
      if(!input || !input.files || input.files.length===0) return alert('No file selected');
      const file = input.files[0];
      try{
        const text = await file.text();
        const rows = this.parseCsv(text);
        if(rows.length===0) return alert('CSV is empty or parse error');
        // first row is header
        const header = rows[0].map(h=>h.trim());
        const entries = rows.slice(1).map(r=>{
          const obj = {};
          for(let i=0;i<header.length;i++){ obj[header[i]] = r[i] ?? ''; }
          return obj;
        }).filter(x=>Object.keys(x).length>0);

        if(entries.length===0) return alert('No data rows found in CSV');

        // confirm
        if(!confirm('Import ' + entries.length + ' contracts from CSV?')) return;

        // process sequentially to avoid overloading server
        let success = 0, failed = 0;
        for(let i=0;i<entries.length;i++){
          const row = entries[i];
          // map known columns (case-insensitive)
          const pick = (names) => {
            for(const n of names){ if(row[n] !== undefined) return row[n]; const low = Object.keys(row).find(k=>k.toLowerCase()===n.toLowerCase()); if(low) return row[low]; }
            return undefined;
          };

          const payload = {
            Title: pick(['Title','title','Название','name']) || '',
            ContractNumber: pick(['ContractNumber','contractNumber','Номер','number']) || '',
            ResponsibleEmployeeId: parseInt(pick(['ResponsibleEmployeeId','responsibleEmployeeId','employeeId','employee','responsible']) || '') || null,
            Amount: parseFloat(pick(['Amount','amount','Сумма'] ) || '') || null,
            Currency: pick(['Currency','currency','Валюта']) || 'RUB',
            ContractType: pick(['ContractType','contractType','Тип','type']) || '',
            Status: pick(['Status','status']) || 'Draft',
            Description: pick(['Description','description','Описание']) || ''
          };

          try{
            const res = await fetch(window.apiBase + '/contracts', { method:'POST', headers: { 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify(payload) });
            const txt = await res.text();
            if(res.ok) { success++; }
            else { failed++; console.warn('Import row failed', i+1, txt); }
          }catch(err){ failed++; console.error('Import error', err); }
        }

        alert('Import completed. Success: ' + success + '. Failed: ' + failed);
        // refresh list
        this.loadContracts();
      }catch(e){ console.error(e); alert('CSV import error: '+ e.message); }
      finally{ try{ e.target.value = null; }catch(_){ } }
    },

    // minimal CSV parser supporting quoted fields
    parseCsv(text){
      if(!text) return [];
      const rows = [];
      let cur = '';
      let row = [];
      let inQuotes = false;
      for(let i=0;i<text.length;i++){
        const ch = text[i];
        const next = text[i+1];
        if(ch === '"'){
          if(inQuotes && next==='"') { cur += '"'; i++; }
          else inQuotes = !inQuotes;
          continue;
        }
        if(ch === ',' && !inQuotes){ row.push(cur); cur=''; continue; }
        if((ch === '\n' || ch === '\r') && !inQuotes){ if(ch === '\r' && next==='\n'){ /* skip \n next */ } row.push(cur); cur=''; if(row.length>1 || row[0]!=='') rows.push(row); row = []; continue; }
        cur += ch;
      }
      // push last
      if(cur!=='' || row.length>0) { row.push(cur); rows.push(row); }
      // trim fields
      return rows.map(r=>r.map(f=> (f||'').trim()));
    },
    // copy token to clipboard with fallback
    async copyToken(){ if(!this.token) return; try{ await navigator.clipboard.writeText(this.token); alert('Token copied'); }catch(e){ window.prompt('Copy token, Ctrl+C, Enter', this.token); } },
    toggleShowToken(){ this.showFullToken = !this.showFullToken; },
    setToken(t, remember=false){
      this.token = t;
      if(remember){
        localStorage.setItem('token', t);
        this.tokenPersisted = true;
      } else {
        localStorage.removeItem('token');
        this.tokenPersisted = false;
      }
    },
    logout(){ 
      this.setToken(''); 
      this.token = ''; 
      this.tokenPersisted = false; 
      localStorage.removeItem('token'); 
      this.contracts = [];
      this.files = [];
      this.allFiles = [];
      this.selectedContract = null;
      this.tags = [];
      this.history = [];
      this.filters = { q: '', status: '', departmentId: '' };
      alert('Logged out'); 
    },
    async login(){
      try{
        const params = new URLSearchParams(); 
        params.append('username', this.auth.username); 
        params.append('password', this.auth.password);

        const res = await fetch(window.apiBase + '/auth/login', { 
          method:'POST', 
          body: params 
        });

        const text = await res.text();
        console.log('Login response status:', res.status);
        console.log('Login response text (raw):', text);

        let j = {};
        try{ 
          j = text ? JSON.parse(text) : {}; 
        }catch(e){ 
          console.error('login json parse error', e);
        }

        if(res.ok && j.access_token){ 
          this.setToken(j.access_token, this.remember); 
          alert('Login successful');
          this.currentPage = 1; 
          await this.loadContracts(); 
        }
        else { 
          const errorMsg = j.error || j.message || 'Unknown error';
          alert('Login failed: ' + errorMsg);
        }
      }catch(e){ 
        console.error('Login exception:', e);
        alert('Login error: '+ e.message); 
      }
    },
    openSwagger(){ window.open('/swagger', '_blank'); this.showSwagger = true; },
    authHeaders(){ return this.token ? { 'Authorization':'Bearer '+this.token } : {}; },
    async loadContracts(){
      try{
        let url = window.apiBase + '/contracts?page=' + this.currentPage + '&pageSize=' + this.pageSize;
        if(this.filters.departmentId) url += '&departmentId=' + encodeURIComponent(this.filters.departmentId);
        if(this.filters.status) url += '&status=' + encodeURIComponent(this.filters.status);
        if(this.filters.q) url += '&q=' + encodeURIComponent(this.filters.q);
        const res = await fetch(url, { headers: { 'Accept': 'application/json; charset=utf-8', ...this.authHeaders() } });
        if(!res.ok){ 
          const err = await res.text(); 
          console.error('loadContracts error:', res.status, err);
          alert('Load contracts failed: ' + res.status + ' ' + (err || 'Unknown error')); 
          return; 
        }
        const text = await res.text();
        console.log('loadContracts raw response:', text);
        const j = JSON.parse(text);
        console.log('loadContracts parsed JSON:', j);
        const rawArr = j.items || j;
        // handle .NET ReferenceHandler.Preserve ($values) or usual arrays
        let listSource = [];
        if (Array.isArray(rawArr)) listSource = rawArr;
        else if (rawArr && rawArr.$values) listSource = rawArr.$values;
        else if (rawArr && rawArr.items) listSource = rawArr.items;
        // normalize PascalCase DTOs to camelCase used by the UI
        const arr = (listSource || []).map(c => ({ id: c.Id ?? c.id, title: c.Title ?? c.title, contractNumber: c.ContractNumber ?? c.contractNumber, partyAId: c.PartyAId ?? c.partyAId, partyBId: c.PartyBId ?? c.partyBId, departmentId: c.DepartmentId ?? c.departmentId, responsibleEmployeeId: c.ResponsibleEmployeeId ?? c.responsibleEmployeeId, contractType: c.ContractType ?? c.contractType, status: c.Status ?? c.status, startDate: c.StartDate ?? c.startDate, endDate: c.EndDate ?? c.endDate, amount: c.Amount ?? c.amount, currency: c.Currency ?? c.currency, description: c.Description ?? c.description, createdAt: c.CreatedAt ?? c.createdAt, createdBy: c.CreatedBy ?? c.createdBy, createdByFullName: c.CreatedByFullName ?? c.createdByFullName }));
        this.contracts = arr;
        this.totalCount = j.totalCount || arr.length;
        try{ console.log('loadContracts: loaded', this.contracts.length, 'items'); }catch(e){}
        // Hide files modal if it was open so contracts list becomes interactable
        this.showAllFiles = false;
      }catch(e){ console.error('loadContracts exception:', e); alert('Failed to load contracts: ' + e.message); }
    },
    clearFilters(){ this.filters = { q:'', status:'', departmentId:'' }; this.currentPage = 1; this.loadContracts(); },
    formatValidationErrors(errorObj) {
      if (typeof errorObj === 'string') return errorObj;
      if (!errorObj) return 'Unknown error';
      const errors = [];
      if (errorObj.errors && typeof errorObj.errors === 'object') {
        for (const [field, messages] of Object.entries(errorObj.errors)) {
          if (Array.isArray(messages)) {
            errors.push(...messages);
          } else if (typeof messages === 'string') {
            errors.push(messages);
          }
        }
      } else if (errorObj.error) {
        errors.push(String(errorObj.error));
      } else if (Array.isArray(errorObj)) {
        errors.push(...errorObj.filter(e => typeof e === 'string'));
      }
      return errors.length > 0 ? errors.join('\n') : JSON.stringify(errorObj);
    },
    safeStringify(obj) {
      try {
        return JSON.stringify(obj, (key, value) => {
          if (typeof value === 'string') {
            return value;
          }
          return value;
        });
      } catch (e) {
        console.error('Stringify error:', e);
        return String(obj);
      }
    },
    async createContract(){
      try{
        const payload = {
          Title: this.newContract.title,
          ContractNumber: this.newContract.contractNumber,
          DepartmentId: this.newContract.departmentId || null,
          ResponsibleEmployeeId: this.newContract.responsibleEmployeeId || null,
          Amount: this.newContract.amount || null,
          Currency: this.newContract.currency || null,
          ContractType: this.newContract.contractType || null,
          Status: this.newContract.status || null,
          StartDate: this.newContract.startDate || null,
          EndDate: this.newContract.endDate || null,
          Description: this.newContract.description || null
        };
        const res = await fetch(window.apiBase + '/contracts', { method:'POST', headers: { 'Content-Type':'application/json; charset=utf-8', ...this.authHeaders() }, body: JSON.stringify(payload) });
         const text = await res.text();
         console.log('Create contract response status:', res.status);
         console.log('Create contract response text:', text);
         if(res.ok){
           let j = {};
           try{ j = text ? JSON.parse(text) : {}; }catch(e){ console.warn('create parse error', e); }
           alert('Contract created, ID=' + (j.contractId || 'unknown'));
           this.newContract = { title:'', contractNumber:'', departmentId:null, responsibleEmployeeId:null, amount:null, description:'', status:'Draft', currency:'RUB', contractType:'', startDate:'', endDate:'' };
           this.loadContracts();
         } else {
           let err = text || 'Unknown error';
           try{ const pj = JSON.parse(text); err = this.formatValidationErrors(pj); }catch(e){ console.warn('error parse failed', e); }
           alert('Error creating contract:\n' + err);
         }
      }catch(e){ alert('Error creating contract: '+e.message); }
    },
    async selectContract(c){
      try{
        const res = await fetch(window.apiBase + '/contracts/' + (c.id || c.Id), { headers: { ...this.authHeaders() } });
        if(!res.ok){ alert('Failed to fetch contract'); return; }
        const j = await res.json();
        const raw = j.response || j;
        // normalize to camelCase
        this.selectedContract = {
          id: raw.Id ?? raw.id,
          title: raw.Title ?? raw.title,
          contractNumber: raw.ContractNumber ?? raw.contractNumber,
          partyAId: raw.PartyAId ?? raw.partyAId,
          partyBId: raw.PartyBId ?? raw.partyBId,
          departmentId: raw.DepartmentId ?? raw.departmentId,
          responsibleEmployeeId: raw.ResponsibleEmployeeId ?? raw.responsibleEmployeeId,
          contractType: raw.ContractType ?? raw.contractType,
          status: raw.Status ?? raw.status,
          startDate: raw.StartDate ?? raw.startDate,
          endDate: raw.EndDate ?? raw.endDate,
          amount: raw.Amount ?? raw.amount,
          currency: raw.Currency ?? raw.currency,
          description: raw.Description ?? raw.description,
          createdAt: raw.CreatedAt ?? raw.createdAt,
          createdBy: raw.CreatedBy ?? raw.createdBy,
          createdByFullName: raw.CreatedByFullName ?? raw.createdByFullName
        };
        // load tags/files/history
        const tRes = await fetch(window.apiBase + `/contracts/${c.id}/files`, { headers: { ...this.authHeaders() } });
        if(tRes.ok){
          const tTxt = await tRes.text();
          console.log('contract files raw:', tTxt);
          let tRaw = [];
          try{ tRaw = tTxt ? JSON.parse(tTxt) : []; } catch(e){ console.error('parse error contract files', e); tRaw = []; }
          const tArr = Array.isArray(tRaw) ? tRaw : (tRaw && tRaw.$values) ? tRaw.$values : [];
          let filesForContract = (tArr || [])
            .filter(f => {
              const fileId = f.Id ?? f.id;
              if (!fileId || fileId === undefined || fileId === null) {
                console.warn('Contract file without ID skipped:', f);
                return false;
              }
              return true;
            })
            .map(f => ({ 
              id: f.Id ?? f.id, 
              displayName: f.DisplayName ?? f.displayName, 
              systemName: f.SystemName ?? f.systemName, 
              employeeId: f.EmployeeId ?? f.employeeId, 
              uploadDate: f.UploadDate ?? f.uploadDate 
            }));
          // also include any file from allFiles that has contractId equal to selected contract
          try{
            const selId = this.selectedContract && this.selectedContract.id ? this.selectedContract.id : (c && (c.id || c.Id));
            if(this.allFiles && this.allFiles.length>0){
              const extra = this.allFiles.filter(x => x.contractId && x.contractId === selId && x.id).map(x => ({ id: x.id, displayName: x.displayName, systemName: x.systemName, employeeId: x.employeeId, uploadDate: x.uploadDate }));
              // merge unique by id
              const byId = {};
              filesForContract.concat(extra).forEach(f => { if(f && f.id) byId[f.id] = f; });
              filesForContract = Object.values(byId);
            }
          }catch(e){ console.error('merge allFiles fallback', e); }
          this.files = filesForContract;
        } else { this.files = []; const txt = await tRes.text(); console.warn('Failed to load contract files:', tRes.status, txt); }
        const hRes = await fetch(window.apiBase + `/contracts/${c.id}/history`, { headers: { ...this.authHeaders() } });
        if(hRes.ok){
          try{
            const txt = await hRes.text();
            const rawH = txt ? JSON.parse(txt) : [];
            const hArr = Array.isArray(rawH) ? rawH : (rawH && rawH.$values) ? rawH.$values : [];
            // Нормализуем в camelCase для Vue компонента
            this.history = (hArr || []).map(h => ({
              id: h.Id ?? h.id,
              action: h.Action ?? h.action,
              performedBy: h.PerformedBy ?? h.performedBy,
              performedAt: h.PerformedAt ?? h.performedAt,
              details: h.Details ?? h.details
            }));
          }catch(e){ console.error('parse history', e); this.history = []; }
        } else this.history = [];
        this.tags = j.tags || [];
      }catch(e){ console.error(e); alert('Error selecting contract'); }
    },
    async addTagToContract(){
      if(!this.selectedContract) return; if(!this.tagToAdd) return;
      try{
        const res = await fetch(window.apiBase + `/contracts/${this.selectedContract.id}/tags`, { method:'POST', headers:{ 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify({ Name: this.tagToAdd }) });
        const txt = await res.text();
        if(res.ok){ const j = txt ? JSON.parse(txt) : {}; console.log('addTag result', j); this.tagToAdd = ''; // refresh contract to get authoritative tags list
          await this.selectContract(this.selectedContract);
        } else { let err = txt; try{ const pj = JSON.parse(txt); err = pj.error || pj; }catch(e){} alert('Error adding tag: '+ (typeof err === 'string' ? err : JSON.stringify(err))); }
      }catch(e){ alert('Error adding tag'); }
    },
    async attachFile(){
      if(!this.selectedContract) return; if(!this.attachFileId) return alert('Specify file ID');
      try{
        const res = await fetch(window.apiBase + `/contracts/${this.selectedContract.id}/attach`, { method:'POST', headers:{ 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify({ FileId: parseInt(this.attachFileId) }) });
        if(res.ok){ alert('File attached'); this.selectContract(this.selectedContract); } else { const j = await res.json(); alert('Error attaching file: '+ JSON.stringify(j)); }
      }catch(e){ alert('Error attaching file'); }
    },

    // Attach a file from All Files list to currently selected contract
    async attachFileFromList(fileId){
      if(!this.selectedContract) return alert('First select a contract');
      try{
        const res = await fetch(window.apiBase + `/contracts/${this.selectedContract.id}/attach`, { method:'POST', headers:{ 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify({ FileId: parseInt(fileId) }) });
        const txt = await res.text();
        if(res.ok){ alert('File attached'); await this.loadAllFiles(); await this.selectContract(this.selectedContract); }
        else { let err = txt; try{ const pj = JSON.parse(txt); err = pj.error || pj; }catch(e){} alert('Error attaching file: ' + (typeof err === 'string' ? err : JSON.stringify(err))); }
      }catch(e){ console.error('attachFileFromList error', e); alert('Error attaching file'); }
    },

    async callAttachFile(fileId){
      return this.attachFileFromList(fileId);
    },

    async uploadMultipart(){
      const input = document.getElementById('fileInput');
      if(!input || !input.files || input.files.length===0) return alert('Select a file');
      const file = input.files[0];
      try{
        // If a contract is selected, use explicit multipart upload including contractId to ensure server binds file to contract
        if(this.selectedContract && this.selectedContract.id){
          await this.uploadMultipartForContract(file, this.uploadEmployeeId, this.selectedContract.id);
        } else {
          await this.uploadFiles([file]);
            }
            input.value = null;
          }catch(e){ alert('Upload error: '+ e.message); }
        },

        // upload single file for a specific contract using fetch (no progress) to ensure contractId included
        async uploadMultipartForContract(file, employeeId, contractId){
      try{
        const fd = new FormData();
        fd.append('file', file);
        if(employeeId) fd.append('employeeId', employeeId);
        if(contractId) fd.append('contractId', contractId);
        const res = await fetch(window.apiBase + '/files/upload-multipart', { method: 'POST', headers: { ...this.authHeaders() }, body: fd });
        const txt = await res.text();
        if(!res.ok){ let err = txt; try{ const pj = JSON.parse(txt); err = pj.error || pj; }catch(e){} throw new Error(typeof err === 'string' ? err : JSON.stringify(err)); }
        let j = {};
        try{ j = txt ? JSON.parse(txt) : {}; }catch(e){}
        alert('File uploaded, ID=' + (j.fileId || '?'));
        // refresh lists and selected contract files
        await this.loadAllFiles();
        if(this.selectedContract) await this.selectContract(this.selectedContract);
      }catch(e){ console.error('uploadMultipartForContract error', e); throw e; }
    },

    async uploadBase64(){
      if(!this.base64FileName) return alert('Specify file name');
      if(!this.base64Content) return alert('Specify base64 content');
      const payload = { FileName: this.base64FileName, Base64Content: this.base64Content, EmployeeId: parseInt(this.uploadEmployeeId) || 0 };
      // If a contract is selected, include contractId
      if(this.selectedContract && this.selectedContract.id) payload.ContractId = this.selectedContract.id;
      try{
        const res = await fetch(window.apiBase + '/files/upload', { method: 'POST', headers: { 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify(payload) });
        const j = await res.json();
          if(res.ok){ alert('File uploaded, ID='+ (j.fileId||'?')); if(this.selectedContract) await this.selectContract(this.selectedContract); }
          else alert('Upload error: '+ JSON.stringify(j));
        }catch(e){ alert('Upload error: '+ e.message); }
    },

    async loadAllFiles(employeeId){
      try{
        let url = window.apiBase + '/files/list';
        if(employeeId) url += '?employeeId=' + encodeURIComponent(employeeId);
        const res = await fetch(url, { headers: { ...this.authHeaders() } });
        // read text first to avoid json parse errors and to surface server messages
        const txt = await res.text();
        console.log('loadAllFiles response', res.status, txt);
        if(!res.ok){
          // try parse JSON error
          let err = txt;
          try{ const j = JSON.parse(txt); err = j.error || txt; }catch(e){}
          alert('Error loading files: ' + res.status + ' ' + err);
          return;
        }
        let raw = [];
        try{ 
          raw = txt ? JSON.parse(txt) : []; 
        } catch(e){ 
          console.error('parse error loadAllFiles', e); 
          alert('Error parsing files: '+ e.message);
          return; 
        }
        const arr = Array.isArray(raw) ? raw : (raw && raw.$values) ? raw.$values : [];

        // Фильтруем файлы с валидными ID и преобразуем в нужный формат
        this.allFiles = (arr || [])
          .filter(f => {
            const fileId = f.Id ?? f.id;
            if (!fileId || fileId === undefined || fileId === null) {
              console.warn('File without ID skipped:', f);
              return false;
            }
            return true;
          })
          .map(f => ({ 
            id: f.Id ?? f.id, 
            displayName: f.DisplayName ?? f.displayName, 
            systemName: f.SystemName ?? f.systemName, 
            employeeId: f.EmployeeId ?? f.employeeId, 
            uploadDate: f.UploadDate ?? f.uploadDate,
            contractId: f.ContractId ?? f.contractId ?? null
          }));
      }catch(e){ 
        console.error('Exception in loadAllFiles:', e);
        alert('Error loading files');
      }
    },

    async onDrop(e){
      const dt = e.dataTransfer;
      if(!dt) return;
      const files = Array.from(dt.files || []);
      if(files.length===0) return;
      await this.uploadFiles(files);
    },

    async uploadFiles(files){
      for(const file of files){
        const entry = { name: file.name, progress: 0 };
        this.uploading.push(entry);
        try{
          const j = await this.uploadWithProgress(file, this.uploadEmployeeId, pct => { entry.progress = pct; });
          entry.result = j;
          // if uploaded and we have a selected contract, try to attach uploaded file to contract
          try{
            const fid = (j && (j.fileId || j.FileId));
            if(fid && this.selectedContract && this.selectedContract.id){
              const attachRes = await fetch(window.apiBase + `/contracts/${encodeURIComponent(this.selectedContract.id)}/attach`, { method: 'POST', headers: { 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify({ FileId: fid }) });
              if(!attachRes.ok){ const atxt = await attachRes.text(); console.warn('Attach after upload failed', attachRes.status, atxt); }
              else {
                // refresh files for selected contract
                await this.selectContract(this.selectedContract);
              }
            }
          }catch(e){ console.warn('Attach after upload error', e); }
        }catch(e){ entry.error = e.message || String(e); }
      }
      // refresh lists
      await this.loadAllFiles();
      if(this.selectedContract) await this.selectContract(this.selectedContract);
      // clear finished
      setTimeout(()=>{ this.uploading = []; }, 1200);
    },

    onModalUpload(){
      const el = document.getElementById('modalFileInput');
      if(!el || !el.files || el.files.length===0) return alert('Select files');
      const arr = Array.from(el.files);
      this.uploadFiles(arr);
      el.value = null;
    },

    uploadWithProgress(file, employeeId, onProgress){
      const self = this; // capture 'this' context for use in xhr callbacks
      return new Promise((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        const url = window.apiBase + '/files/upload-multipart';
        xhr.open('POST', url, true);
        const token = this.token;
        if(token) xhr.setRequestHeader('Authorization', 'Bearer ' + token);
        xhr.upload.onprogress = function(e){ if(e.lengthComputable && onProgress) onProgress(Math.round(e.loaded*100 / e.total)); };
        xhr.onreadystatechange = function(){
          if(xhr.readyState !== 4) return;
          if(xhr.status >=200 && xhr.status < 300){
            try{ const j = JSON.parse(xhr.responseText); resolve(j); }catch(e){ resolve({}); }
          } else {
            reject(new Error(xhr.responseText || ('Status '+xhr.status)));
          }
        };
        const fd = new FormData();
        fd.append('file', file);
        if(employeeId) fd.append('employeeId', employeeId);
        // if a contract is selected, attach uploaded file to that contract immediately
        try{ if(self.selectedContract && self.selectedContract.id) fd.append('contractId', self.selectedContract.id); }catch(_){}
        xhr.send(fd);
      });
    },

    async pasteBase64FromClipboard(){
      try{
        const txt = await navigator.clipboard.readText();
        this.base64Content = txt;
        alert('Pasted from clipboard');
      }catch(e){ alert('Clipboard read error'); }
    },

    async downloadFile(fileId){
      try{
        if (!fileId || fileId === undefined || fileId === null || fileId === 'undefined') {
          alert('Error: File ID is invalid or missing');
          return;
        }
        const res = await fetch(window.apiBase + '/files/download?fileId=' + encodeURIComponent(fileId), { headers: { ...this.authHeaders() } });
        if(!res.ok){ const txt = await res.text(); alert('Download error: '+txt); return; }
        const blob = await res.blob();
        const f = (this.files || []).find(x => x.id === fileId) || (this.allFiles || []).find(x => x.id === fileId) || {};
        const name = f.displayName || ('file_'+fileId);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
      }catch(e){ alert('Download error: '+ e.message); }
    },

    async deleteFile(fileId){
      if(!confirm('Delete file id='+fileId+'?')) return;
      try{
        // server expects FileId property
        const res = await fetch(window.apiBase + '/files/delete', { method:'POST', headers: { 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify({ FileId: fileId }) });
        const j = await res.json();
        if(res.ok){ 
          alert('File deleted');
          await this.loadAllFiles(); 
          if(this.selectedContract) await this.selectContract(this.selectedContract); 
        }
        else alert('Error deleting: '+ JSON.stringify(j));
      }catch(e){ alert('Error deleting: '+ e.message); }
    },
    async exportSelected(){
      if(!this.selectedContract) return;
      try{
        const id = this.selectedContract.id;
        const res = await fetch(window.apiBase + '/contracts/' + encodeURIComponent(id) + '/export', { headers: { ...this.authHeaders() } });
        if(!res.ok){ const txt = await res.text(); alert('Export error: '+txt); return; }
        const blob = await res.blob();
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `contract_${id}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
      }catch(e){ alert('Export error: '+ e.message); }
    },

    async deleteSelected(){
      if(!this.selectedContract) return; if(!confirm('Delete contract?')) return;
      try{
        const id = this.selectedContract.id;
        const res = await fetch(window.apiBase + `/contracts/${encodeURIComponent(id)}`, { method:'DELETE', headers: { ...this.authHeaders() } });
        if(res.ok){ alert('Deleted'); this.selectedContract = null; this.loadContracts(); }
        else {
          const txt = await res.text();
          alert('Delete error: ' + txt);
        }
      }catch(e){ alert('Delete error: '+ e.message); }
    },
    async updateContract(){
      if(!this.selectedContract) return;
      try{
        const payload = {
          Title: this.selectedContract.title,
          ContractNumber: this.selectedContract.contractNumber,
          PartyAId: this.selectedContract.partyAId || null,
          PartyBId: this.selectedContract.partyBId || null,
          DepartmentId: this.selectedContract.departmentId || null,
          ResponsibleEmployeeId: this.selectedContract.responsibleEmployeeId || null,
          ContractType: this.selectedContract.contractType || null,
          Status: this.selectedContract.status || null,
          StartDate: this.selectedContract.startDate || null,
          EndDate: this.selectedContract.endDate || null,
          Amount: this.selectedContract.amount || null,
          Currency: this.selectedContract.currency || null,
          Description: this.selectedContract.description || null
        };
        const res = await fetch(window.apiBase + `/contracts/${this.selectedContract.id}`, { method:'PUT', headers: { 'Content-Type':'application/json', ...this.authHeaders() }, body: JSON.stringify(payload) });
        if(res.ok){ alert('Saved'); this.selectContract(this.selectedContract); this.loadContracts(); } else { const txt = await res.text(); let err = txt; try{ const pj = JSON.parse(txt); err = this.formatValidationErrors(pj); }catch(e){} alert('Save error:\n' + err); }
      }catch(e){ alert('Save error: ' + e.message); }
    },
    prevPage(){ if(this.currentPage>1){ this.currentPage--; this.loadContracts(); } },
    nextPage(){ if(this.currentPage < this.totalPages){ this.currentPage++; this.loadContracts(); } }
  },
  mounted(){ if(this.token) this.loadContracts(); }
});

app.mount('#app');

// Attach native fallback for modal upload button in case Vue binding is blocked by overlay
try {
  const attachBtn = document.getElementById('modalUploadBtn');
  if (attachBtn) {
    attachBtn.addEventListener('click', function (e) {
      try { console.log('modalUploadBtn clicked (native fallback)'); } catch (_) { }
      // call Vue method if app is mounted
      try {
        if (window && window.appInstance && typeof window.appInstance.onModalUpload === 'function')
          window.appInstance.onModalUpload();
      } catch (e) {
        try {
          if (app && app._instance && app._instance.proxy && typeof app._instance.proxy.onModalUpload === 'function')
            app._instance.proxy.onModalUpload();
        } catch (_) { }
      }
    });
  }
} catch (e) { console.error('attach fallback listener error', e); }

// expose instance for fallback calls
try { if (app && app._instance && app._instance.proxy) window.appInstance = app._instance.proxy; } catch (e) {}

// Provide global fallback for attachFileFromList in case template resolves to global scope
try{
  window.attachFileFromList = function(fileId){
    try{ if(window.appInstance && typeof window.appInstance.attachFileFromList === 'function') return window.appInstance.attachFileFromList(fileId); }
    catch(e){ console.error('global attachFileFromList fallback error', e); }
    console.warn('attachFileFromList handler not available');
  };
}catch(e){ console.error('attachFileFromList global fallback init error', e); }


