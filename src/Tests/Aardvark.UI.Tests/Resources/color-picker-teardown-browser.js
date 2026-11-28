(async function () {
    const result={lifecycle:[],plugin:[],drag:[],compatibility:[],packets:[],errors:[],completed:0};
    const packets=[], destroyCalls=[];
    let bubble=null, trace=[];
    const check=(group,ok,message)=>{if(!ok)result[group].push(message);};
    const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
    const delay=()=>new Promise(resolve=>setTimeout(resolve,20));
    const execute=code=>{const script=document.createElement('script');script.textContent=code;document.body.appendChild(script);script.remove();};
    window.WebSocket=class{constructor(){window.socket=this;this.readyState=1;}send(text){if(text!=='#ping'){const p=JSON.parse(text);packets.push(p);if(bubble&&p.args)trace.push('send');}}};
    aardvark.connect('events');socket.onopen();
    const handlers=()=>[document,window].flatMap(target=>Object.entries($._data(target,'events')||{}).flatMap(([type,entries])=>entries.map(h=>({target,type,h}))));
    const extra=before=>handlers().filter(entry=>!before.has(entry.h));
    const snapshot=()=>new Set(handlers().map(entry=>entry.h));
    // Diagnostic cleanup after observations keeps failures in the ORIGINAL isolated from later cases.
    const cleanHandlers=before=>{for(const {target,type,h} of extra(before))$(target).off(type+(h.namespace?'.'+h.namespace:''),h.handler);};
    const ui=()=>document.querySelectorAll('.sp-container,.sp-replacer').length;
    const colors=()=>packets.filter(p=>p.name==='data-event'&&p.args);
    const create=async row=>{
        execute(row.initial);aardvark.addReferences(row.references,()=>execute(row.create));await aardvark.promise;
        const input=document.getElementById(row.id);
        if(!input||$(input).data('spectrum.id')===undefined)throw new Error('Actual updater did not initialize '+row.id);
        socket.onmessage({data:JSON.stringify({targetId:row.id,channel:'valueCh',data:row.channelInitial})});await aardvark.promise;
        if(row.mode==='open')$(input).spectrum('show');
        return input;
    };
    const push=async(row,data)=>{socket.onmessage({data:JSON.stringify({targetId:row.id,channel:'valueCh',data})});await aardvark.promise;};
    const palette=input=>{
        $(input).spectrum('show');
        const swatch=$(input).spectrum('container').find('.sp-palette .sp-thumb-el').first()[0];
        if(!swatch)throw new Error('Missing rendered palette');
        swatch.dispatchEvent(new MouseEvent('click',{bubbles:true,cancelable:true}));
    };
    const drag=(input,rail)=>{
        $(input).spectrum('show');
        const part=$(input).spectrum('container').find('.sp-'+rail)[0], rect=part.getBoundingClientRect();
        if(rect.width<=0||rect.height<=0)throw new Error('Drag rail not laid out');
        part.dispatchEvent(new MouseEvent('mousedown',{bubbles:true,cancelable:true,clientX:rect.left+rect.width*.7,clientY:rect.top+rect.height*.4,button:0,buttons:1}));
    };
    const nativeAfterRemoval=()=>{
        document.dispatchEvent(new MouseEvent('mousemove',{bubbles:true,cancelable:true,clientX:200,clientY:200,buttons:1}));
        document.dispatchEvent(new MouseEvent('mouseup',{bubbles:true,cancelable:true,button:0}));
        window.dispatchEvent(new Event('resize'));
        document.dispatchEvent(new KeyboardEvent('keydown',{bubbles:true,key:'Escape',keyCode:27}));
    };
    window.beforeBubble=(receiver,event)=>{
        if(receiver!==bubble.host||event.currentTarget!==bubble.host||event.target!==bubble.child)throw new Error('Wrong bubbling receiver');
        trace.push('before');
    };
    window.afterBubble=()=>trace.push('after');
    try {
        const survivorRow=fixtures[0].data, survivor=await create(survivorRow);
        const original=$.fn.spectrum;
        $.fn.spectrum=function(...args){if(args[0]==='destroy')this.each(function(){destroyCalls.push({id:this.id,attached:this.isConnected});});return original.apply(this,args);};
        Object.assign($.fn.spectrum,original);
        const baseUI=ui(), base=snapshot();
        for(const fixture of fixtures.slice(1)){
            const row=fixture.data, group=fixture.group.startsWith('drag-')?'drag':fixture.group;
            const protectDrag=group==='plugin'&&row.mode==='closed';
            if(protectDrag)drag(survivor,'color');
            const before=snapshot(), beforeUI=ui();
            let input=null, container=null, replacer=null, dragStops=0;
            try {
                input=await create(row);container=$(input).spectrum('container');replacer=$(input).next('.sp-replacer');
                if(group==='compatibility'){
                    check(group,$(input).spectrum('get').toHex8()==='44112233','Initial channel preserves RGBA');
                    await push(row,row.channelUpdate);
                    check(group,$(input).spectrum('get').toHex8()==='cc6496c8','Adaptive channel preserves RGBA/alpha');
                    packets.length=0;palette(input);
                    check(group,colors().length===1,'Normal palette selection sends exactly once');
                    result.packets.push(...colors());
                    const host=document.getElementById(row.root),child=document.createElement('span');
                    child.style.cssText='position:absolute;left:50px;top:20px;width:40px;height:30px';host.appendChild(child);
                    bubble={host,child};trace=[];
                    const bounds=host.getBoundingClientRect.bind(host);host.getBoundingClientRect=()=>{trace.push('bounds:host');return bounds();};
                    const childBounds=child.getBoundingClientRect.bind(child);child.getBoundingClientRect=()=>{trace.push('bounds:child');return childBounds();};
                    child.addEventListener('mousemove',e=>{if(e.currentTarget!==child||e.target!==child)throw new Error('Wrong child receiver');trace.push('child');});
                    document.addEventListener('mousemove',e=>{if(e.currentTarget!==document||e.target!==child)throw new Error('Wrong document receiver');trace.push('document');},{once:true});
                    packets.length=0;
                    const e=new MouseEvent('mousemove',{bubbles:true,cancelable:true,clientX:111,clientY:83});
                    check(group,child.dispatchEvent(e)&&!e.defaultPrevented,'Bubbling default policy');
                    check(group,same(trace,['child','before','bounds:host','send','after','document']),'Distinct-child receiver/geometry/side-effect order: '+JSON.stringify(trace));
                    check(group,packets.filter(p=>p.args).length===1,'One bubbling packet');
                    result.packets.push(...packets.filter(p=>p.args));bubble=null;
                }
                if(group==='drag'){
                    const [,rail,phase]=fixture.group.split('-');
                    $(input).on('dragstop.spectrum',()=>dragStops++);
                    drag(input,rail);
                    if(phase!=='active')document.dispatchEvent(new MouseEvent('mouseup',{bubbles:true,cancelable:true,button:0}));
                    if(phase==='doublepending'){
                        drag(input,rail);
                        document.dispatchEvent(new MouseEvent('mouseup',{bubbles:true,cancelable:true,button:0}));
                    }
                }
                packets.length=0;
                const calls=destroyCalls.length;
                if(group==='plugin'){
                    $(input).spectrum('destroy');
                    check(group,$(input).data('spectrum.id')===undefined,'Registry handle removed for '+row.mode);
                }
                execute(row.remove);
                check(group,!document.getElementById(row.id),'Generated updater removes input');
                if(group==='lifecycle'||group==='drag'){
                    const owned=destroyCalls.slice(calls).filter(call=>call.id===row.id);
                    check(group,owned.length===1&&owned[0].attached,'Shutdown destroys instance before removing '+row.mode+' input');
                }
                check(group,extra(before).length===0,'Owned global handlers removed immediately: '+fixture.group+'/'+row.mode+' remaining '+extra(before).length);
                const retained=new Set(handlers().map(entry=>entry.h));
                check(group,[...before].every(h=>retained.has(h)),'Surviving picker global handlers remain registered');
                if(protectDrag)check(group,document.body.classList.contains('sp-dragging'),'Unrelated active drag retains body class');
                nativeAfterRemoval();await delay();
                check(group,colors().filter(p=>p.sender===row.id).length===0,'No post-removal color messages: '+fixture.group+'/'+row.mode);
                if(protectDrag)check(group,colors().some(p=>p.sender===survivorRow.id),'Surviving active drag completes normally');
                if(group==='drag')check(group,dragStops===0,'No retired drag-stop callbacks including queued completions');
                check(group,ui()===beforeUI,'No orphan Spectrum UI: '+fixture.group+'/'+row.mode+' remaining '+(ui()-beforeUI));
                check(group,extra(before).length===0,'No owned document/window handlers: '+fixture.group+'/'+row.mode+' remaining '+extra(before).length);
                // The independently initialized inline picker retains handlers and selection.
                $(survivor).spectrum('set','#abcdef');packets.length=0;palette(survivor);
                check(group,colors().length===1&&colors()[0].sender===survivorRow.id,'Surviving picker still dispatches independently');
                result.packets.push(...colors());
            }catch(error){result[group].push(String(error));}
            finally{
                if(input)$(input).spectrum('destroy');
                if(container)container.remove();if(replacer)replacer.remove();
                await delay();cleanHandlers(before);execute(row.finish);result.completed++;
            }
        }
        check('compatibility',ui()===baseUI&&extra(base).length===0,'Repeated lifecycle retention stays at surviving-picker baseline');
        execute(survivorRow.remove);execute(survivorRow.finish);result.completed++;
    }catch(error){result.errors.push(String(error));}
    result.errors.push(...scriptErrors);
    const pre=document.createElement('pre');pre.id='picker-results';pre.textContent=JSON.stringify(result);document.body.appendChild(pre);
})();
