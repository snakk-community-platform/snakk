"use strict";(function(){"use strict";async function i(){try{const e=await(await fetch("/bff/auth/status",{credentials:"include"})).json();d(e),e.isAuthenticated&&e.publicId?(window.currentUserId=e.publicId,p(e),r(),u()):c(),document.dispatchEvent(new CustomEvent("snakk:nav:loaded",{detail:{authenticated:e.isAuthenticated,user:e}}))}catch(t){const e=t instanceof Error?t.message:"Unknown error";console.warn("[Auth Navbar] Failed to fetch auth status:",t),c(),d({isAuthenticated:!1,error:e})}finally{window.snakkTheme?.updateToggleButton()}}function p(t){const e=document.getElementById("auth-nav");if(!e||!t.publicId||!t.displayName)return;const a=t.emailVerified?"":'<span class="badge badge-warning badge-xs ml-2">Unverified</span>';e.innerHTML=`
            <!-- Notification Bell -->
            <div class="dropdown dropdown-end mr-2">
                <label tabindex="0" class="btn btn-ghost btn-sm btn-circle relative">
                    <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
                    </svg>
                    <span id="notification-badge" class="notification-badge hidden">0</span>
                </label>
                <div tabindex="0" class="dropdown-content z-[1] mt-3 w-80 max-h-96 overflow-y-auto shadow-lg bg-white border border-subtle rounded-lg">
                    <div class="flex items-center justify-between p-3 border-b border-subtle">
                        <span class="font-semibold">Notifications</span>
                        <button data-action="mark-all-notifications-read" class="text-xs text-primary hover:underline">Mark all read</button>
                    </div>
                    <div id="notification-list" class="p-2">
                        <p class="text-sm text-muted text-center py-4">Loading...</p>
                    </div>
                </div>
            </div>
            <!-- User Menu -->
            <div class="dropdown dropdown-end">
                <label tabindex="0" class="btn btn-ghost btn-sm btn-circle p-0">
                    <div class="avatar avatar-sm">
                        <img src="${t.avatarUrl}"
                             alt="${o(t.displayName)}"
                             loading="lazy" />
                    </div>
                </label>
                <ul tabindex="0" class="mt-3 z-[1] p-2 shadow-lg menu menu-sm dropdown-content bg-white border border-subtle rounded-lg w-52">
                    <li>
                        <a href="/u/${t.publicId}" class="font-semibold">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                            </svg>
                            ${o(t.displayName)}
                            ${a}
                        </a>
                    </li>
                    <li>
                        <a href="/settings">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                            </svg>
                            Settings
                        </a>
                    </li>
                    <li>
                        <a href="#" id="theme-toggle" data-action="toggle-theme">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
                            </svg>
                            Toggle Theme
                        </a>
                    </li>
                    ${window.UserRoleType?.hasModeratorPrivileges(t.role)?`
                    <li><hr class="my-1 border-subtle"/></li>
                    <li>
                        <a href="/moderation">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                            </svg>
                            Moderation
                        </a>
                    </li>
                    <li><hr class="my-1 border-subtle"/></li>
                    `:""}
                    ${window.UserRoleType?.isGlobalAdmin(t.role)?`
                    <li>
                        <a href="/admin">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                            </svg>
                            Admin Panel
                        </a>
                    </li>
                    <li><hr class="my-1 border-subtle"/></li>
                    `:""}
                    <li>
                        <a href="#" data-action="logout">
                            <svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
                            </svg>
                            Logout
                        </a>
                    </li>
                </ul>
            </div>
        `,window.snakkTheme?.updateToggleButton()}function c(){const t=document.getElementById("auth-nav");t&&(t.innerHTML=`
            <a href="/auth/login" class="btn btn-ghost btn-sm">Login</a>
            <a href="/auth/register" class="btn btn-primary btn-sm">Sign Up</a>
        `)}function d(t){const e=document.getElementById("debug-auth-info");if(e)if(t.isAuthenticated&&t.displayName&&t.publicId){const a=t.emailVerified?'<span class="text-green-400">verified</span>':'<span class="text-orange-400">unverified</span>',n=t.role?`<span class="text-gray-500">|</span>
                   <span class="text-gray-400">Role:</span>
                   <span class="text-purple-400 font-semibold uppercase">${o(t.role)}</span>`:"";e.innerHTML=`
                <span class="text-gray-400">Auth:</span>
                <span class="text-green-300">logged in</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">User:</span>
                <span class="text-cyan-300">${o(t.displayName)}</span>
                <span class="text-gray-600">(${t.publicId})</span>
                <span class="text-gray-500">|</span>
                <span class="text-gray-400">Email:</span>
                ${a}
                ${n}
            `}else e.innerHTML=`
                <span class="text-gray-400">Auth:</span>
                <span class="text-red-400">not logged in</span>
                ${t.error?`<span class="text-gray-600">(${o(t.error)})</span>`:""}
            `}async function g(){try{await fetch("/bff/auth/logout",{method:"POST",credentials:"include"})}catch(t){console.warn("[Auth Navbar] Logout error:",t)}finally{window.location.replace("/")}}async function r(){try{const e=await(await fetch("/bff/notifications/unread-count",{credentials:"include"})).json();s(e.count)}catch(t){console.warn("[Auth Navbar] Failed to load notification count:",t)}}function s(t){const e=document.getElementById("notification-badge");e&&(t>0?(e.textContent=t>99?"99+":t.toString(),e.classList.remove("hidden")):e.classList.add("hidden"))}async function u(){const t=document.getElementById("notification-list");if(t)try{const a=await(await fetch("/bff/notifications?offset=0&pageSize=10",{credentials:"include"})).json();if(!a.items||a.items.length===0){t.innerHTML='<p class="text-sm text-muted text-center py-4">No notifications yet</p>';return}t.innerHTML=a.items.map(n=>`
                <div class="notification-item ${n.isRead?"":"unread"}" data-id="${n.publicId}">
                    <div class="flex items-start gap-2 p-2 rounded hover:bg-subtle cursor-pointer"
                         data-action="click-notification"
                         data-notification-id="${n.publicId}"
                         data-discussion-id="${n.sourceDiscussionId||""}">
                        <div class="notification-icon ${v(n.type)}">
                            ${b(n.type)}
                        </div>
                        <div class="flex-1 min-w-0">
                            <p class="text-sm font-medium truncate">${o(n.title)}</p>
                            ${n.body?`<p class="text-xs text-muted line-clamp-2">${o(n.body)}</p>`:""}
                            <p class="text-xs text-muted mt-1">${k(n.createdAt)}</p>
                        </div>
                    </div>
                </div>
            `).join("")}catch(e){console.warn("[Auth Navbar] Failed to load notifications:",e),t.innerHTML='<p class="text-sm text-error text-center py-4">Failed to load</p>'}}async function w(t){const e=t.dataset.notificationId,a=t.dataset.discussionId;if(e)try{await fetch(`/bff/notifications/${e}/read`,{method:"POST",credentials:"include"}),r();const n=document.querySelector(`[data-id="${e}"]`);n&&n.classList.remove("unread")}catch(n){console.warn("[Auth Navbar] Failed to mark notification as read:",n)}}async function m(){try{await fetch("/bff/notifications/read-all",{method:"POST",credentials:"include"}),s(0),document.querySelectorAll(".notification-item.unread").forEach(t=>{t.classList.remove("unread")})}catch(t){console.warn("[Auth Navbar] Failed to mark all notifications as read:",t)}}function v(t){return{Reply:"text-primary",Mention:"text-accent"}[t]||"text-muted"}function b(t){return{Reply:'<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6" /></svg>',Mention:'<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 12a4 4 0 10-8 0 4 4 0 008 0zm0 0v1.5a2.5 2.5 0 005 0V12a9 9 0 10-9 9m4.5-1.206a8.959 8.959 0 01-4.5 1.207" /></svg>',NewPostInFollowedDiscussion:'<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 8h10M7 12h4m1 8l-4-4H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-3l-4 4z" /></svg>'}[t]||'<svg xmlns="http://www.w3.org/2000/svg" class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" /></svg>'}function k(t){const e=new Date(t),n=new Date().getTime()-e.getTime(),l=Math.floor(n/6e4),h=Math.floor(n/36e5),f=Math.floor(n/864e5);return l<1?"just now":l<60?`${l}m ago`:h<24?`${h}h ago`:f<7?`${f}d ago`:e.toLocaleDateString()}function o(t){if(!t)return"";const e=document.createElement("div");return e.textContent=t,e.innerHTML}document.addEventListener("click",async t=>{const a=t.target.closest("[data-action]");if(!a)return;switch(t.preventDefault(),a.dataset.action){case"logout":await g();break;case"toggle-theme":window.snakkTheme?.toggleTheme();break;case"mark-all-notifications-read":await m();break;case"click-notification":await w(a);break}}),document.addEventListener("snakk:realtime:notification-count",t=>{s(t.detail.unreadCount)}),document.addEventListener("snakk:realtime:notification",()=>{r(),u()}),document.readyState==="loading"?document.addEventListener("DOMContentLoaded",i):i(),window.SnakkAuthNav={refresh:i,updateNotificationBadge:s}})();
